module ArbitrageGainer.Program

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open TradingStrategy
open System
open DotNetEnv
open PolygonStarterCode1.PolygonWebSocket // Import PolygonWebSocket module
open PolygonStarterCode1

let webApp =
    choose [
        GET >=> route "/" >=> text "Hello World from Giraffe!"
        TradingStrategyApp().WebApp
    ]

[<EntryPoint>]
let main _ =
    // Load environment variables
    Env.Load() |> ignore
    let apiKey = Environment.GetEnvironmentVariable("API_KEY")
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
                .UseUrls("http://0.0.0.0:8080")
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
