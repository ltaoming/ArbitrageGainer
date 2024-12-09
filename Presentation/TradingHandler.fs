namespace Presentation.TradingHandler

module TradingHandler =
    open ArbitrageGainer.Logging.OrderLogger
    open Giraffe
    open Microsoft.AspNetCore.Http
    open RealTimeMarketData.PolygonWebSocket
    open System
    open System.Net.Http
    open System.Text.Json
    open FSharp.Control.Tasks
    open ArbitrageGainer.HistoryArbitrageOpportunity
    open ArbitrageGainer.Services.Repository.TradingStrategyRepository
    open ArbitrageGainer.Logging.AnalysisLogger
    open TradingAlgorithm.TradingAlgorithm

    type TradingStrategyResponse = {
        Id: string
        NumberOfCurrencies: int
        MinimalPriceSpread: float
        MinTransactionProfit: float
        MaximalTransactionValue: float
        MaximalTradingValue: float
        InitInvestment: float
    }

    let performHistoricalAnalysis() =
        let data = loadData ()
        calculateHistoryArbitrageOpportunity data
        |> Seq.map (fun line ->
            let parts = line.Split(',')
            parts.[0].Trim()
        )
        |> Seq.toList

    let getTradingStrategyParams() = task {
        use httpClient = new HttpClient()
        let! response = httpClient.GetAsync("http://localhost:8000/trading-strategy")
        response.EnsureSuccessStatusCode() |> ignore
        let! json = response.Content.ReadAsStringAsync()
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        let strategyData = JsonSerializer.Deserialize<TradingStrategyResponse>(json, options)
        return strategyData
    }

    let startTradingHandler (cancellationToken: System.Threading.CancellationToken): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                orderLogger "Time to First Order Start"

                // Fetch the trading strategy parameters from the endpoint
                let! strategyData = getTradingStrategyParams()
                let tradingParams = {
                    MinimalPriceSpreadValue = strategyData.MinimalPriceSpread
                    MinimalTransactionProfit = strategyData.MinTransactionProfit
                    MaximalTotalTransactionValue = strategyData.MaximalTransactionValue
                    MaximalTradingValue = strategyData.MaximalTradingValue
                }

                // Update the trading params in the cache agent
                updateTradingParams tradingParams

                let numberOfPairs = strategyData.NumberOfCurrencies

                AnalysisLogger "Historical Arbitrage Analysis Start"
                // Perform historical analysis
                let historicalPairs = performHistoricalAnalysis()
                printfn "%A" historicalPairs
                AnalysisLogger "Historical Arbitrage Analysis End"

                // Get cross-traded pairs by making an HTTP GET request to /cross-traded-pairs
                let crossTradedPairs = getCrossTradedPairsFromDb()

                // Determine which currency pairs to track
                let pairsToTrack =
                    historicalPairs
                    |> List.filter (fun pair -> List.contains pair crossTradedPairs)
                    |> List.truncate numberOfPairs

                // Start the subscriptions
                printfn "Starting subscriptions for pairs: %A" pairsToTrack

                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e"
                let testUri = Uri("wss://one8656-live-data.onrender.com/")
                let subscriptionParametersList =
                    pairsToTrack
                    |> List.map (fun pair -> "XQ." + pair)

                let connectionTasks =
                    subscriptionParametersList
                    |> List.map (fun param ->
                        start (testUri, apiKey, param, cancellationToken)
                    )

                Async.Parallel connectionTasks
                |> Async.Ignore
                |> Async.Start

                return! json pairsToTrack next ctx
            }

    let stopTradingHandler (cancelTrading: unit -> unit): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                // This will trigger cancellation of all ongoing operations
                cancelTrading()
                return! text "Trading activities stopped." next ctx
            }
