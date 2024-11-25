namespace Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Presentation.Handlers
open Presentation.Handlers
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open System
open Presentation.CrossTradePairHandler
open Application
open Presentation.TradingHandler
open ArbitrageGainer.AnnualizedReturnCalc
open ArbitrageGainer.Services.Repository.TradingStrategyRepository 
open ArbitrageGainer.Database  
open Presentation.PNLHandler

module Program =
    let webApp (agent: TradingStrategyAgent): HttpHandler =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
            POST >=> route "/start-trading" >=> TradingHandler.startTradingHandler
            Presentation.Handlers.createWebApp agent
            AnnualizedReturnApp(agent).WebApp
            PNLHandler.PNLWebApp
        ]

    [<EntryPoint>]
    let main args =
        printfn "WebSocket connections will start upon '/start-trading' endpoint call."
        let isConnected = testMongoDBConnection()
        if isConnected then
            printfn "MongoDB connection test passed."
        else
            printfn "MongoDB connection test failed."

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