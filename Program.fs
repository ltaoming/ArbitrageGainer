namespace ArbitrageGainer.Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open TradingStrategy
open CrossTradedPairs 
open Presentation.Handlers 
open System
open RealTimeMarketData 

module Program =

    let webApp =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
            Presentation.Handlers.webApp  
        ]

    [<EntryPoint>]
    let main args =
        let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e" 
        let uri = Uri("wss://socket.polygon.io/crypto")
        // Define multiple subscription parameters
        let subscriptionParametersList = [
            "XT.BTC-USD"
            "XT.ETH-USD"
            "XT.LTC-USD"
        ]

        // Create a list of asynchronous start operations for each subscription
        let connectionTasks =
            subscriptionParametersList
            |> List.map (fun params ->
                PolygonWebSocket.start (uri, apiKey, params)
            )

        // Run all connections concurrently
        Async.Parallel connectionTasks
        |> Async.Ignore
        |> Async.Start

        printfn "Started multiple WebSocket connections."
        
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(fun webHost ->
                webHost
                    .UseUrls("http://0.0.0.0:8000")
                    .ConfigureServices(fun services ->
                        services.AddGiraffe() |> ignore
                    )
                    .Configure(fun app ->
                        app.UseGiraffe webApp)
                |> ignore
            )
            .Build()
            .Run()
           
        0
