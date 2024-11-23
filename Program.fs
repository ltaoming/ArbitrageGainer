namespace ArbitrageGainer.Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Presentation.Handlers
open System
open Presentation.CrossTradePairHandler
open RealTimeMarketData
open ArbitrageGainer.AnnualizedReturnCalc
open Presentation.TradingHandler

module Program =
    let webApp =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
            POST >=> route "/start-trading" >=> TradingHandler.startTradingHandler
            Presentation.Handlers.webApp
            AnnualizedReturnApp().WebApp
        ]

    [<EntryPoint>]
    let main args =
        printfn "WebSocket connections will start upon '/start-trading' endpoint call."
        
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