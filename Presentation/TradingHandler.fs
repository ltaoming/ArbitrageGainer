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
    

    type StartTradingRequest = {
        NumberOfPairs: int
    }

    let performHistoricalAnalysis() =
        // let dataPath = "../../../historicalData.txt"
        // let data = loadData dataPath
        // calculateHistoryArbitrageOpportunity data
        // |> Seq.toList
        ["BTC-USD"; "ETH-USD"; "LTC-USD"; "XRP-USD"; "BCH-USD"]

    let startTradingHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                // start trading endpoint
                orderLogger "Time to First Order Start"
                let! startTradingRequest = ctx.BindJsonAsync<StartTradingRequest>()
                let numberOfPairs = startTradingRequest.NumberOfPairs

                // Perform historical analysis
                let historicalPairs = performHistoricalAnalysis()

                // Get cross-traded pairs by making an HTTP GET request to /cross-traded-pairs
                let crossTradedPairs = getCrossTradedPairsFromDb ()

                // Determine which currency pairs to track
                let pairsToTrack =
                    historicalPairs
                    |> List.filter (fun pair -> List.contains pair crossTradedPairs)
                    |> List.truncate numberOfPairs

                // Start the subscriptions
                printfn "Starting subscriptions for pairs: %A" pairsToTrack

                // Start the subscriptions
                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e"
                let uri = Uri("wss://socket.polygon.io/crypto")
                let testUri = Uri("wss://one8656-live-data.onrender.com/")
                let subscriptionParametersList =
                    pairsToTrack
                    |> List.map (fun pair -> "XQ." + pair)

                let connectionTasks =
                    subscriptionParametersList
                    |> List.map (fun params ->
                        start (testUri, apiKey, params)
                    )

                Async.Parallel connectionTasks
                |> Async.Ignore
                |> Async.Start

                return! json pairsToTrack next ctx
            }