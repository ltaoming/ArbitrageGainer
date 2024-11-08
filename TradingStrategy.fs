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

type TradingStrategyError =
    | FileSaveError of string
    | FileLoadError of string
    | InvalidStrategy of string

let strategyFilePath = "strategy.json"

// Pure function: update strategy logic without side effects
let updateStrategyPure (strategy: TradingStrategy) : Result<TradingStrategy, TradingStrategyError> =
    // More business logic validation can be added here
    Ok strategy

// Function to save strategy to file (with side effects)
let saveStrategyToFile (strategy: TradingStrategy) : Result<unit, TradingStrategyError> =
    try
        let json = JsonSerializer.Serialize(strategy)
        File.WriteAllText(strategyFilePath, json)
        Ok ()
    with
    | ex -> Error (FileSaveError $"Failed to save strategy to file: {ex.Message}")

// Function to load strategy from file (with side effects)
let loadStrategyFromFile () : Result<TradingStrategy option, TradingStrategyError> =
    match File.Exists(strategyFilePath) with
    | true ->
        try
            let json = File.ReadAllText(strategyFilePath)
            Ok (Some (JsonSerializer.Deserialize<TradingStrategy>(json)))
        with
        | ex -> Error (FileLoadError $"Failed to load strategy from file: {ex.Message}")
    | false -> Ok None

// Function to handle strategy update (combining pure logic and side effects)
let saveAndSetCurrentStrategy (logger: ILogger) (strategy: TradingStrategy option) : Result<string, TradingStrategyError> =
    match strategy with
    | Some updatedStrategy ->
        match saveStrategyToFile updatedStrategy with
        | Ok () ->
            logger.LogInformation("Updated strategy: {@Strategy}", updatedStrategy)
            Ok "Strategy updated successfully"
        | Error err ->
            logger.LogError("Failed to update the strategy: {Error}", err)
            Error err
    | None -> Error (InvalidStrategy "Invalid strategy provided")

// HTTP handler for updating trading strategy
let updateTradingStrategyHandler: HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger()
            try
                let! strategy = ctx.BindJsonAsync<TradingStrategy>()
                match updateStrategyPure strategy with
                | Ok updatedStrategy ->
                    match saveAndSetCurrentStrategy logger (Some updatedStrategy) with
                    | Ok response ->
                        return! text response next ctx
                    | Error (FileSaveError msg) ->
                        return! RequestErrors.BAD_REQUEST msg next ctx
                    | Error (InvalidStrategy msg) ->
                        return! RequestErrors.BAD_REQUEST msg next ctx
                    | Error (FileLoadError msg) ->
                        return! RequestErrors.BAD_REQUEST msg next ctx
                | Error (InvalidStrategy msg) ->
                    return! RequestErrors.BAD_REQUEST msg next ctx
            with ex ->
                logger.LogError(ex, "Unexpected error while processing POST request")
                return! RequestErrors.BAD_REQUEST "Unexpected server error" next ctx
        }

// HTTP handler for getting trading strategy
let getTradingStrategyHandler: HttpHandler =
    fun next ctx ->
        let logger = ctx.GetLogger()
        match loadStrategyFromFile() with
        | Ok (Some strategy) ->
            logger.LogInformation("Received GET request for current strategy")
            json strategy next ctx
        | Ok None ->
            logger.LogWarning("No strategy defined yet")
            RequestErrors.NOT_FOUND "No strategy defined yet" next ctx
        | Error (FileLoadError msg) ->
            logger.LogError("Failed to load the strategy from file: {Error}", msg)
            RequestErrors.BAD_REQUEST msg next ctx

type TradingStrategyApp () =
    member _.WebApp =
        choose [
            POST >=> route "/trading-strategy" >=> updateTradingStrategyHandler
            GET >=> route "/trading-strategy" >=> getTradingStrategyHandler
        ]
