namespace Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open ArbitrageGainer.Services.Repository.TradingStrategyRepository
open ArbitrageGainer.Database
open Application
open Presentation.PNLHandler
open Presentation.TradingHandler // Ensure this is opened
open ArbitrageGainer.AnnualizedReturnCalc
open Presentation.Handlers

module Program =
    let webApp (agent: TradingStrategyAgent): HttpHandler =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/cross-traded-pairs" >=> Presentation.CrossTradePairHandler.getCrossTradedPairsHandler
            POST >=> route "/start-trading" >=> TradingHandler.startTradingHandler agent // fully qualify the call
            createWebApp agent
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
                        app.UseGiraffe (webApp agent)
                    )
                |> ignore
            )
            .Build()
            .Run()
        
        0