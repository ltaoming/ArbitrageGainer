namespace Presentation.TradingHandler

module TradingHandler =
    open Giraffe
    open Microsoft.AspNetCore.Http
    open RealTimeMarketData.PolygonWebSocket
    open System
    open System.Net.Http
    open System.Text.Json
    open FSharp.Control.Tasks
    open Application

    type StartTradingRequest = {
        NumberOfPairs: int
    }

    let performHistoricalAnalysis() =
        // Placeholder function
        // Should perform historical analysis and return a list of currency pairs
        // For now, we simulate that based on some logic
        // Let's assume that historical analysis returns the top 5 currency pairs
        ["BTC-USD"; "ETH-USD"; "LTC-USD"; "XRP-USD"; "BCH-USD"]
    
    let startTradingHandler (agent: TradingStrategyAgent): HttpHandler =
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
                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e" // Should be stored securely
                let uri = Uri("wss://socket.polygon.io/crypto")
                let subscriptionParametersList =
                    pairsToTrack
                    |> List.map (fun pair -> "XT." + pair)

                let connectionTasks =
                    subscriptionParametersList
                    |> List.map (fun parameters ->
                        start (uri, apiKey, parameters)
                    )

                Async.Parallel connectionTasks
                |> Async.Ignore
                |> Async.Start

                // Set the start date of trading if not already set
                let! startDateOpt = agent.GetStartDateOfTrading()
                match startDateOpt with
                | None ->
                    agent.SetStartDateOfTrading(DateTime.Now)
                | Some _ -> ()

                return! json pairsToTrack next ctx
            }