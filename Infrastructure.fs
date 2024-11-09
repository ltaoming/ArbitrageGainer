namespace Infrastructure

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open Domain

module FileRepository =
    // Implementation of ITradingStrategyRepository
    type FileTradingStrategyRepository(filePath: string) =
        interface ITradingStrategyRepository with
            member _.Save(strategy: TradingStrategy) =
                try
                    let dto = {
                        NumberOfCurrencies = let (CurrencyCount v) = strategy.NumberOfCurrencies in Some v
                        MinimalPriceSpread = let (PriceSpread v) = strategy.MinimalPriceSpread in Some v
                        MaximalTransactionValue = let (TransactionValue v) = strategy.MaximalTransactionValue in Some v
                        MaximalTradingValue = let (TradingValue v) = strategy.MaximalTradingValue in Some v
                    }
                    let jsonOptions = JsonSerializerOptions()
                    jsonOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
                    jsonOptions.Converters.Add(JsonFSharpConverter())
                    let json = JsonSerializer.Serialize(dto, jsonOptions)
                    File.WriteAllText(filePath, json)
                    Ok ()
                with
                | ex -> Error (RepositoryError ex.Message)

            member _.Load() =
                try
                    if File.Exists(filePath) then
                        let json = File.ReadAllText(filePath)
                        let jsonOptions = JsonSerializerOptions()
                        jsonOptions.Converters.Add(JsonFSharpConverter())
                        let dto = JsonSerializer.Deserialize<TradingStrategyDto>(json, jsonOptions)
                        match Validation.updateStrategyPure dto with
                        | Ok strategy -> Ok (Some strategy)
                        | Error err -> Error err
                    else
                        Ok None
                with
                | ex -> Error (RepositoryError ex.Message)
