namespace Infrastructure

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open ArbitrageGainer.Services.Repository.TradingStrategyRepository
open Domain

module FileRepository =
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization
    open FSharp.SystemTextJson
    open MongoDB.Bson
    open ArbitrageGainer.Core

    // JsonSerializerOptions as an immutable configuration
    let private jsonOptions = 
        let options = JsonSerializerOptions()
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.Converters.Add(JsonFSharpConverter())
        options

    let private serializeTradingStrategy (strategy: TradingStrategy) =
        let dto = {
            NumberOfCurrencies = strategy.NumberOfCurrencies
            MinimalPriceSpread = strategy.MinimalPriceSpread
            MinTransactionProfit = strategy.MinTransactionProfit
            MaximalTransactionValue = strategy.MaximalTransactionValue
            MaximalTradingValue = strategy.MaximalTradingValue
            InitInvestment = 
                match strategy.InitInvestment with
                | InitialInvestment v -> v
        }
        JsonSerializer.Serialize(dto, jsonOptions)

    let private deserializeTradingStrategy (json: string) =
        JsonSerializer.Deserialize<TradingStrategyDto>(json, jsonOptions)
        |> Validation.updateStrategyPure

    let saveToFile (filePath: string) (strategy: TradingStrategy): Result<unit, TradingStrategyError> =
        try
            let json = serializeTradingStrategy strategy
            File.WriteAllText(filePath, json)
            Ok ()
        with
        | ex -> Error (TradingStrategyError.RepositoryError ex.Message)

    let loadFromFile (filePath: string): Result<TradingStrategy option, TradingStrategyError> =
        try
            match File.Exists(filePath) with
            | true ->
                let json = File.ReadAllText(filePath)
                match deserializeTradingStrategy json with
                | Ok strategy -> Ok (Some strategy)
                | Error err -> Error err
            | false -> Ok None
        with
        | ex -> Error (TradingStrategyError.RepositoryError ex.Message)

    // Define repository functions as a class implementing the interface
    type TradingStrategyRepository(strategyFilePath: string) =
        interface ITradingStrategyRepository with
            member _.Save(strategy: TradingStrategy) = saveToFile strategyFilePath strategy
            member _.Load() = loadFromFile strategyFilePath

    // Factory function to create a file-based repository
    let createFileRepository (filePath: string): ITradingStrategyRepository =
        TradingStrategyRepository(filePath) :> ITradingStrategyRepository
