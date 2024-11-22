namespace ArbitrageGainer.Program

open Giraffe
open MarketDataInfra
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open CrossTradedPairs 
open Presentation.Handlers 
open System
open ArbitrageGainer.AnnualizedReturnCalc
open MarketDataInfra

module Program =
    let webApp =
        choose [
            GET >=> route "/" >=> text "Hello World from Giraffe!"
            GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
            GET >=> route "/start-trading" >=> StartTradingHandlers.startTradingHandler
        ]

    [<EntryPoint>]
    let main args =
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