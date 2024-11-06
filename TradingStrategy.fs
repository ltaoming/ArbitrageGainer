module TradingStrategy

open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

[<CLIMutable>]
type TradingStrategy = {
    [<JsonPropertyName("NumberOfCurrencies")>]
    NumberOfCurrencies: int
    [<JsonPropertyName("MinimalPriceSpread")>]
    MinimalPriceSpread: float
    [<JsonPropertyName("MaximalTransactionValue")>]
    MaximalTransactionValue: float
    [<JsonPropertyName("MaximalTradingValue")>]
    MaximalTradingValue: float
}

let strategyFilePath = "strategy.json"

let saveStrategyToFile (strategy: TradingStrategy) =
    let json = JsonSerializer.Serialize(strategy)
    File.WriteAllText(strategyFilePath, json)

let loadStrategyFromFile () =
    if File.Exists(strategyFilePath) then
        let json = File.ReadAllText(strategyFilePath)
        Some (JsonSerializer.Deserialize<TradingStrategy>(json))
    else
        None

let mutable currentStrategy: TradingStrategy option = loadStrategyFromFile()

let updateStrategy (logger: ILogger) (strategy: TradingStrategy) =
    currentStrategy <- Some strategy
    saveStrategyToFile strategy
    logger.LogInformation("Updated strategy: {@Strategy}", strategy)
    "Strategy updated successfully"

let updateTradingStrategyHandler: HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger()
            try
                let! strategy = ctx.BindJsonAsync<TradingStrategy>()
                logger.LogInformation("Successfully deserialized strategy: {@Strategy}", strategy)
                let response = updateStrategy logger strategy
                return! text response next ctx
            with ex ->
                logger.LogError(ex, "Error while processing POST request")
                return! RequestErrors.BAD_REQUEST "Invalid parameters" next ctx
        }

let getTradingStrategyHandler: HttpHandler =
    fun next ctx ->
        let logger = ctx.GetLogger()
        match currentStrategy with
        | Some strategy ->
            logger.LogInformation("Received GET request for current strategy")
            json strategy next ctx
        | None ->
            logger.LogWarning("No strategy defined yet")
            RequestErrors.NOT_FOUND "No strategy defined yet" next ctx

type TradingStrategyApp () =
    member _.WebApp =
        choose [
            POST >=> route "/trading-strategy" >=> updateTradingStrategyHandler
            GET >=> route "/trading-strategy" >=> getTradingStrategyHandler
        ]