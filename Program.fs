namespace Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Presentation.Handlers
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open System
open Presentation.CrossTradePairHandler
open Application.TradingStrategyAgent // Access TradingStrategyAgent
open Presentation.TradingHandler
open ArbitrageGainer.AnnualizedReturnCalc
open ArbitrageGainer.Services.Repository.TradingStrategyRepository
open ArbitrageGainer.Database
open Presentation.PNLHandler
open Presentation.TestEmailHandler
open System.Threading

module Program =
    let cts = new CancellationTokenSource()

    let webApp (agent: TradingStrategyAgent): HttpHandler =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/test-email" >=> testEmailHandler
            GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
            POST >=> route "/start-trading" >=> TradingHandler.startTradingHandler cts.Token
            POST >=> route "/stop-trading" >=> TradingHandler.stopTradingHandler (fun () -> cts.Cancel())
            Presentation.Handlers.createWebApp agent
            AnnualizedReturnApp(agent).WebApp
            PNLHandler.PNLWebApp
        ]

    [<EntryPoint>]
    let main args =
        printfn "WebSocket connections will start upon '/start-trading' endpoint call."
        let isConnected = testMongoDBConnection()
        match isConnected with
        | true -> printfn "MongoDB connection test passed."
        | false -> printfn "MongoDB connection test failed."


        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(fun webHost ->
                webHost
                    .UseUrls("http://0.0.0.0:8000")
                    .ConfigureServices(fun services ->
                        services.AddGiraffe() |> ignore
                        services.AddSingleton<TradingStrategyAgent>(fun provider ->
                            let logger = provider.GetRequiredService<ILogger<obj>>() // Use ILogger<obj> or a known type
                            // Create the agent using the factory function
                            let typedLogger = logger :?> ILogger // cast if needed
                            let agent = createTradingStrategyAgent typedLogger
                            agent
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