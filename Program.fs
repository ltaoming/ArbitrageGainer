open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open TradingStrategy

let webApp =
    choose [
        GET >=> route "/" >=> text "Hello World from Giraffe!"
        TradingStrategyApp().WebApp
    ]

[<EntryPoint>]
let main _ =
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
