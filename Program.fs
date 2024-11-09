module ArbitrageGainer.Program

open Giraffe
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open TradingStrategy
open CrossTradedPairs 
open System
open RealTimeMarketData


let webApp =
    choose [
        GET >=> route "/" >=> text "Hello World from Giraffe!"
        GET >=> route "/cross-traded-pairs" >=> getCrossTradedPairsHandler
        TradingStrategyApp().WebApp
    ]

[<EntryPoint>]
let main args =
    let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e"
    let uri = Uri("wss://socket.polygon.io/crypto")
    let subscriptionParameters = "XT.BTC-USD"
    
    // Start WebSocket client
    async {
        do! PolygonWebSocket.start (uri, apiKey, subscriptionParameters)
    } |> Async.Start

    // Start Giraffe server
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
    