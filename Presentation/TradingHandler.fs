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

    type StartTradingRequest = {
        NumberOfPairs: int
    }

    let performHistoricalAnalysis() =
        let data = loadData ()
        calculateHistoryArbitrageOpportunity data
        |> Seq.toList
        ["BTC-USD"; "ETH-USD"; "LTC-USD"; "XRP-USD"; "BCH-USD"]

    let startTradingHandler (cancellationToken: System.Threading.CancellationToken): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                orderLogger "Time to First Order Start"
                let! startTradingRequest = ctx.BindJsonAsync<StartTradingRequest>()
                let numberOfPairs = startTradingRequest.NumberOfPairs

                AnalysisLogger "AnalysisTime to First Order Start"
                // Perform historical analysis
                let historicalPairs = performHistoricalAnalysis()
                AnalysisLogger "AnalysisTime to First Order End"
                // Get cross-traded pairs by making an HTTP GET request to /cross-traded-pairs
                let crossTradedPairs = getCrossTradedPairsFromDb ()

                // Determine which currency pairs to track
                let pairsToTrack =
                    historicalPairs
                    |> List.filter (fun pair -> List.contains pair crossTradedPairs)
                    |> List.truncate numberOfPairs

                // Start the subscriptions
                printfn "Starting subscriptions for pairs: %A" pairsToTrack

                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e"
                let uri = Uri("wss://socket.polygon.io/crypto")
                let testUri = Uri("wss://one8656-live-data.onrender.com/")
                let subscriptionParametersList =
                    pairsToTrack
                    |> List.map (fun pair -> "XQ." + pair)

                let connectionTasks =
                    subscriptionParametersList
                    |> List.map (fun params ->
                        start (testUri, apiKey, params, cancellationToken)
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
