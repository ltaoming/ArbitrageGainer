namespace Infrastructure

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open Domain

module FileRepository =
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization
    open FSharp.SystemTextJson
    open Domain

    // Define DTO for TradingStrategy
    type TradingStrategyDto = {
        NumberOfCurrencies: int option
        MinimalPriceSpread: decimal option
        MaximalTransactionValue: decimal option
        MaximalTradingValue: decimal option
    }

    // JsonSerializerOptions as an immutable configuration
    let private jsonOptions = 
        let options = JsonSerializerOptions()
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.Converters.Add(JsonFSharpConverter())
        options

    let private serializeTradingStrategy (strategy: TradingStrategy) =
        let dto = {
            NumberOfCurrencies = let (CurrencyCount v) = strategy.NumberOfCurrencies in Some v
            MinimalPriceSpread = let (PriceSpread v) = strategy.MinimalPriceSpread in Some (decimal v)
            MaximalTransactionValue = let (TransactionValue v) = strategy.MaximalTransactionValue in Some (decimal v)
            MaximalTradingValue = let (TradingValue v) = strategy.MaximalTradingValue in Some (decimal v)
        }
        JsonSerializer.Serialize(dto, jsonOptions)

    let private deserializeTradingStrategy (json: string) =
        JsonSerializer.Deserialize<Domain.TradingStrategyDto>(json, jsonOptions)
        |> Validation.updateStrategyPure

    let saveToFile (filePath: string) (strategy: TradingStrategy): Result<unit, Domain.TradingStrategyError> =
        try
            let json = serializeTradingStrategy strategy
            File.WriteAllText(filePath, json)
            Ok ()
        with
        | ex -> Error (Domain.TradingStrategyError.RepositoryError ex.Message)

    let loadFromFile (filePath: string): Result<TradingStrategy option, Domain.TradingStrategyError> =
        try
            match File.Exists(filePath) with
            | true -> 
                let json = File.ReadAllText(filePath)
                match deserializeTradingStrategy json with
                | Ok strategy -> Ok (Some strategy)
                | Error err -> Error err
            | false -> Ok None
        with
        | ex -> Error (Domain.TradingStrategyError.RepositoryError ex.Message)

    // Define repository functions as a record
    type TradingStrategyRepository = {
        Save: TradingStrategy -> Result<unit, TradingStrategyError>
        Load: unit -> Result<TradingStrategy option, TradingStrategyError>
    }

    // Factory function to create a file-based repository
    let createFileRepository (filePath: string): TradingStrategyRepository =
        {
            Save = saveToFile filePath
            Load = fun () -> loadFromFile filePath
        }