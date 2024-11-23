namespace Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Presentation.Handlers 
open System
open Application

module Program =
    open RealTimeMarketData
    let webApp (agent: TradingStrategyAgent): HttpHandler =
        choose [
            POST >=> route "/trading-strategy" >=> updateTradingStrategyHandler agent
            GET >=> route "/trading-strategy" >=> getTradingStrategyHandler agent
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
                        services.AddSingleton<TradingStrategyAgent>(fun provider ->
                            let logger = provider.GetRequiredService<ILogger<TradingStrategyAgent>>()
                            TradingStrategyAgent(logger)
                        ) |> ignore
                    )
                    .Configure(fun app ->
                        let agent = app.ApplicationServices.GetService(typeof<TradingStrategyAgent>) :?> TradingStrategyAgent
                        app.UseGiraffe (webApp agent))
                |> ignore
            )
            .Build()
            .Run()
           
        0