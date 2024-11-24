namespace Infrastructure

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open Domain

module FileRepository =
    // JsonSerializerOptions as an immutable configuration
    let private jsonOptions = 
        let options = JsonSerializerOptions()
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.Converters.Add(JsonFSharpConverter())
        options

    let private serializeTradingStrategy (strategy: TradingStrategy) =
        let dto = {
            NumberOfCurrencies = let (CurrencyCount v) = strategy.NumberOfCurrencies in Some v
            MinimalPriceSpread = let (PriceSpread v) = strategy.MinimalPriceSpread in Some v
            MaximalTransactionValue = let (TransactionValue v) = strategy.MaximalTransactionValue in Some v
            MaximalTradingValue = let (TradingValue v) = strategy.MaximalTradingValue in Some v
            InitialInvestmentAmount = let (InitialInvestment v) = strategy.InitialInvestmentAmount in Some v
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
        | ex -> Error (RepositoryError ex.Message)

    let loadFromFile (filePath: string): Result<TradingStrategy option, TradingStrategyError> =
        try
            if File.Exists(filePath) then
                let json = File.ReadAllText(filePath)
                match deserializeTradingStrategy json with
                | Ok strategy -> Ok (Some strategy)
                | Error err -> Error err
            else
                Ok None
        with
        | ex -> Error (RepositoryError ex.Message)

    // Define repository interface
    type ITradingStrategyRepository =
        abstract member Save : TradingStrategy -> Result<unit, TradingStrategyError>
        abstract member Load : unit -> Result<TradingStrategy option, TradingStrategyError>

    // Define repository functions as a class implementing the interface
    type TradingStrategyRepository(strategyFilePath: string) =
        interface ITradingStrategyRepository with
            member _.Save(strategy: TradingStrategy) = saveToFile strategyFilePath strategy
            member _.Load() = loadFromFile strategyFilePath

    // Factory function to create a file-based repository
    let createFileRepository (filePath: string): ITradingStrategyRepository =
        TradingStrategyRepository(filePath) :> ITradingStrategyRepository
