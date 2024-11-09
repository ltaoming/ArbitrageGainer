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
    | InvalidInputError of string

let strategyFilePath = "strategy.json"

// Pure function: update strategy logic without side effects
let updateStrategyPure (strategy: TradingStrategy) : Result<TradingStrategy, TradingStrategyError> =
    match strategy with
    | _ when strategy.NumberOfCurrencies <= 0 -> Error (InvalidStrategy "Number of currencies must be greater than zero")
    | _ when strategy.MinimalPriceSpread <= 0.0 -> Error (InvalidStrategy "Minimal price spread must be greater than zero")
    | _ when strategy.MaximalTransactionValue <= 0.0 -> Error (InvalidStrategy "Maximal transaction value must be greater than zero")
    | _ when strategy.MaximalTradingValue <= 0.0 -> Error (InvalidStrategy "Maximal trading value must be greater than zero")
    | _ when strategy.MaximalTransactionValue < strategy.MinimalPriceSpread -> Error (InvalidStrategy "Maximal transaction value must be greater than or equal to the minimal price spread")
    | _ -> Ok strategy

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

// Railroad-oriented function to handle strategy update and saving
let saveAndSetCurrentStrategy (logger: ILogger) (strategy: TradingStrategy) : Result<string, TradingStrategyError> =
    strategy
    |> updateStrategyPure
    |> Result.bind (fun updatedStrategy ->
        saveStrategyToFile updatedStrategy
        |> Result.map (fun _ -> "Strategy updated successfully"))
    |> function
        | Error err ->
            logger.LogError("Failed to update and save the strategy: {Error}", err)
            Error err
        | Ok successMessage -> Ok successMessage

// HTTP handler for updating trading strategy
let updateTradingStrategyHandler: HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger()
            try
                let! strategyResult =
                    task {
                        try
                            let! strategy = ctx.BindJsonAsync<TradingStrategy>()
                            return Ok strategy
                        with ex ->
                            logger.LogError(ex, "Invalid input data format. Please check the parameter names and types.")
                            return Error (InvalidInputError "Invalid input data format. Please check the parameter names and types.")
                    }
                match strategyResult with
                | Ok strategy ->
                    match saveAndSetCurrentStrategy logger strategy with
                    | Ok response -> return! text response next ctx
                    | Error (InvalidInputError msg) -> return! RequestErrors.BAD_REQUEST (sprintf "%A" msg) next ctx
                    | Error err -> return! RequestErrors.BAD_REQUEST (sprintf "%A" err) next ctx
                | Error (InvalidInputError msg) -> return! RequestErrors.BAD_REQUEST (sprintf "%A" msg) next ctx
                | Error err -> return! RequestErrors.BAD_REQUEST (sprintf "%A" err) next ctx
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
