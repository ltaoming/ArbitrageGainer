namespace Presentation.TradingHandler

module TradingHandler =
    open Giraffe
    open Microsoft.AspNetCore.Http
    open RealTimeMarketData.PolygonWebSocket
    open System
    open System.Net.Http
    open System.Text.Json
    open FSharp.Control.Tasks
    open ArbitrageGainer.HistoryArbitrageOpportunity

    type StartTradingRequest = {
        NumberOfPairs: int
    }

    let performHistoricalAnalysis() =
        let dataPath = "../../../historicalData.txt"
        let data = loadData dataPath
        calculateHistoryArbitrageOpportunity data
        |> Seq.toList

    let startTradingHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! startTradingRequest = ctx.BindJsonAsync<StartTradingRequest>()
                let numberOfPairs = startTradingRequest.NumberOfPairs

                // Perform historical analysis
                let historicalPairs = performHistoricalAnalysis()

                // Get cross-traded pairs by making an HTTP GET request to /cross-traded-pairs
                use httpClient = new HttpClient()
                let! response = httpClient.GetAsync("http://localhost:8000/cross-traded-pairs")
                response.EnsureSuccessStatusCode() |> ignore
                let! content = response.Content.ReadAsStringAsync()
                let crossTradedPairs = JsonSerializer.Deserialize<string list>(content)

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