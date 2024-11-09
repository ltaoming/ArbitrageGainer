module TradingStrategy

open FSharp.Control.Tasks
open Giraffe
open Giraffe.Core
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open System.Threading.Tasks

// Domain Types
type CurrencyCount = CurrencyCount of int
type PriceSpread = PriceSpread of float
type TransactionValue = TransactionValue of float
type TradingValue = TradingValue of float

// Validation Error Types
type ValidationError =
    | MissingField of string
    | NumberOfCurrenciesMustBePositive
    | MinimalPriceSpreadMustBePositive
    | MaximalTransactionValueMustBePositive
    | MaximalTradingValueMustBePositive
    | MaximalTransactionValueLessThanMinimalPriceSpread

// Domain Model
type TradingStrategy = {
    NumberOfCurrencies: CurrencyCount
    MinimalPriceSpread: PriceSpread
    MaximalTransactionValue: TransactionValue
    MaximalTradingValue: TradingValue
}

// DTO for Deserialization
[<CLIMutable>]
type TradingStrategyDto = {
    [<JsonPropertyName("NumberOfCurrencies")>]
    NumberOfCurrencies: int option
    [<JsonPropertyName("MinimalPriceSpread")>]
    MinimalPriceSpread: float option
    [<JsonPropertyName("MaximalTransactionValue")>]
    MaximalTransactionValue: float option
    [<JsonPropertyName("MaximalTradingValue")>]
    MaximalTradingValue: float option
}

// Error Type for Application
type TradingStrategyError =
    | FileSaveError of string
    | FileLoadError of string
    | ValidationErrors of ValidationError list
    | InvalidInputError of string

// Custom Result Computation Expression
type ResultBuilder() =
    member _.Bind(m, f) =
        match m with
        | Ok x -> f x
        | Error e -> Error e
    member _.Return(x) = Ok x

let result = ResultBuilder()

// Validation Function
let updateStrategyPure (dto: TradingStrategyDto) : Result<TradingStrategy, TradingStrategyError> =
    result {
        let! currencyCount =
            match dto.NumberOfCurrencies with
            | Some v when v > 0 -> Ok (CurrencyCount v)
            | Some _ -> Error (ValidationErrors [NumberOfCurrenciesMustBePositive])
            | None -> Error (ValidationErrors [MissingField "NumberOfCurrencies"])

        let! priceSpread =
            match dto.MinimalPriceSpread with
            | Some v when v > 0.0 -> Ok (PriceSpread v)
            | Some _ -> Error (ValidationErrors [MinimalPriceSpreadMustBePositive])
            | None -> Error (ValidationErrors [MissingField "MinimalPriceSpread"])

        let! transactionValue =
            match dto.MaximalTransactionValue with
            | Some v when v > 0.0 -> Ok (TransactionValue v)
            | Some _ -> Error (ValidationErrors [MaximalTransactionValueMustBePositive])
            | None -> Error (ValidationErrors [MissingField "MaximalTransactionValue"])

        let! tradingValue =
            match dto.MaximalTradingValue with
            | Some v when v > 0.0 -> Ok (TradingValue v)
            | Some _ -> Error (ValidationErrors [MaximalTradingValueMustBePositive])
            | None -> Error (ValidationErrors [MissingField "MaximalTradingValue"])

        let (TransactionValue txValue) = transactionValue
        let (PriceSpread psValue) = priceSpread

        do!
            match txValue >= psValue with
            | true -> Ok ()
            | false -> Error (ValidationErrors [MaximalTransactionValueLessThanMinimalPriceSpread])

        return {
            NumberOfCurrencies = currencyCount
            MinimalPriceSpread = priceSpread
            MaximalTransactionValue = transactionValue
            MaximalTradingValue = tradingValue
        }
    }

// Pure Serialization Function
let serializeStrategy (strategy: TradingStrategy) : string =
    let dto = {
        NumberOfCurrencies = let (CurrencyCount v) = strategy.NumberOfCurrencies in Some v
        MinimalPriceSpread = let (PriceSpread v) = strategy.MinimalPriceSpread in Some v
        MaximalTransactionValue = let (TransactionValue v) = strategy.MaximalTransactionValue in Some v
        MaximalTradingValue = let (TradingValue v) = strategy.MaximalTradingValue in Some v
    }
    let jsonOptions = JsonSerializerOptions()
    jsonOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    jsonOptions.Converters.Add(JsonFSharpConverter())
    JsonSerializer.Serialize(dto, jsonOptions)

// Impure File Writing Function with Explicit Dependency
let writeStrategyToFile (filePath: string) (json: string) : Result<unit, TradingStrategyError> =
    try
        File.WriteAllText(filePath, json)
        Ok ()
    with
    | ex -> Error (FileSaveError ex.Message)

// Function to Save Strategy to File
let saveStrategyToFile (filePath: string) (strategy: TradingStrategy) : Result<unit, TradingStrategyError> =
    let json = serializeStrategy strategy
    writeStrategyToFile filePath json

// Pure Deserialization Function
let deserializeStrategy (json: string) : Result<TradingStrategy, TradingStrategyError> =
    try
        let jsonOptions = JsonSerializerOptions()
        jsonOptions.Converters.Add(JsonFSharpConverter())
        let dto = JsonSerializer.Deserialize<TradingStrategyDto>(json, jsonOptions)
        updateStrategyPure dto
    with
    | ex -> Error (FileLoadError ex.Message)

// Impure File Reading Function with Explicit Dependency
let readStrategyFromFile (filePath: string) : Result<string, TradingStrategyError> =
    try
        match File.Exists(filePath) with
        | true ->
            let json = File.ReadAllText(filePath)
            Ok json
        | false ->
            Error (FileLoadError "Strategy file not found")
    with
    | ex -> Error (FileLoadError ex.Message)

// Function to Load Strategy from File
let loadStrategyFromFile (filePath: string) : Result<TradingStrategy option, TradingStrategyError> =
    match readStrategyFromFile filePath with
    | Ok json ->
        match deserializeStrategy json with
        | Ok strategy -> Ok (Some strategy)
        | Error err -> Error err
    | Error (FileLoadError _) -> Ok None // File not found is not an error here
    | Error err -> Error err

// Function to Handle Strategy Update and Saving
let saveAndSetCurrentStrategy (logger: ILogger) (filePath: string) (dto: TradingStrategyDto) : Result<string, TradingStrategyError> =
    updateStrategyPure dto
    |> Result.bind (fun strategy ->
        saveStrategyToFile filePath strategy
        |> Result.map (fun _ -> "Strategy updated successfully"))
    |> function
        | Ok message -> Ok message
        | Error err ->
            match err with
            | ValidationErrors errs ->
                errs |> List.iter (fun e ->
                    match e with
                    | MissingField msg -> logger.LogError("Validation error: {Message}", msg)
                    | NumberOfCurrenciesMustBePositive -> logger.LogError("Validation error: NumberOfCurrencies must be positive")
                    | MinimalPriceSpreadMustBePositive -> logger.LogError("Validation error: MinimalPriceSpread must be positive")
                    | MaximalTransactionValueMustBePositive -> logger.LogError("Validation error: MaximalTransactionValue must be positive")
                    | MaximalTradingValueMustBePositive -> logger.LogError("Validation error: MaximalTradingValue must be positive")
                    | MaximalTransactionValueLessThanMinimalPriceSpread -> logger.LogError("Validation error: MaximalTransactionValue must be greater than or equal to MinimalPriceSpread")
                )
            | FileSaveError msg -> logger.LogError("File save error: {Message}", msg)
            | FileLoadError msg -> logger.LogError("File load error: {Message}", msg)
            | InvalidInputError msg -> logger.LogError("Invalid input error: {Message}", msg)
            Error err

// Custom JSON Binder
let bindJsonAsync<'T> (ctx: HttpContext) : Task<'T> =
    task {
        let! body = ctx.ReadBodyFromRequestAsync()
        let jsonOptions = JsonSerializerOptions()
        jsonOptions.Converters.Add(JsonFSharpConverter())
        return JsonSerializer.Deserialize<'T>(body, jsonOptions)
    }

// HTTP Handler for Updating Trading Strategy
let updateTradingStrategyHandler (strategyFilePath: string): HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger()
            let! dtoResult =
                task {
                    try
                        let! dto = bindJsonAsync<TradingStrategyDto> ctx
                        return Ok dto
                    with ex ->
                        logger.LogError(ex, "Invalid input data format.")
                        return Error (InvalidInputError "Invalid input data format.")
                }
            match dtoResult with
            | Ok dto ->
                match saveAndSetCurrentStrategy logger strategyFilePath dto with
                | Ok message -> return! text message next ctx
                | Error (ValidationErrors errs) ->
                    let messages = errs |> List.map (function
                        | MissingField msg -> msg
                        | NumberOfCurrenciesMustBePositive -> "NumberOfCurrencies must be greater than zero"
                        | MinimalPriceSpreadMustBePositive -> "MinimalPriceSpread must be greater than zero"
                        | MaximalTransactionValueMustBePositive -> "MaximalTransactionValue must be greater than zero"
                        | MaximalTradingValueMustBePositive -> "MaximalTradingValue must be greater than zero"
                        | MaximalTransactionValueLessThanMinimalPriceSpread -> "MaximalTransactionValue must be greater than or equal to MinimalPriceSpread"
                    )
                    return! RequestErrors.BAD_REQUEST (String.concat "; " messages) next ctx
                | Error (InvalidInputError msg) -> return! RequestErrors.BAD_REQUEST msg next ctx
                | Error err -> return! ServerErrors.INTERNAL_ERROR (sprintf "%A" err) next ctx
            | Error (InvalidInputError msg) -> return! RequestErrors.BAD_REQUEST msg next ctx
            | Error err -> return! ServerErrors.INTERNAL_ERROR (sprintf "%A" err) next ctx
        }

// HTTP Handler for Getting Trading Strategy
let getTradingStrategyHandler (strategyFilePath: string): HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger()
            match loadStrategyFromFile strategyFilePath with
            | Ok (Some strategy) ->
                let json = serializeStrategy strategy
                ctx.SetContentType "application/json"
                return! text json next ctx
            | Ok None ->
                return! RequestErrors.NOT_FOUND "No strategy defined yet" next ctx
            | Error err ->
                logger.LogError("Error loading strategy: {Error}", err)
                return! ServerErrors.INTERNAL_ERROR (sprintf "%A" err) next ctx
        }

// Application Web App
type TradingStrategyApp () =
    member _.WebApp =
        let strategyFilePath = "strategy.json"
        choose [
            POST >=> route "/trading-strategy" >=> updateTradingStrategyHandler strategyFilePath
            GET >=> route "/trading-strategy" >=> getTradingStrategyHandler strategyFilePath
        ]
