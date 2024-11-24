namespace Presentation

open Microsoft.AspNetCore.Http
open Giraffe
open Giraffe.Core
open FSharp.Control.Tasks
open System.Threading.Tasks
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson
open Application
open Domain
open Microsoft.Extensions.Logging

module Handlers =
    open ArbitrageGainer.Core
    open MongoDB.Bson

    // Custom JSON Binder
    let bindJsonAsync<'T> (ctx: HttpContext) : Task<'T> =
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let jsonOptions = JsonSerializerOptions()
            jsonOptions.Converters.Add(JsonFSharpConverter())
            return JsonSerializer.Deserialize<'T>(body, jsonOptions)
        }

    // HTTP Handler for Updating Trading Strategy
    let updateTradingStrategyHandler (agent: TradingStrategyAgent): HttpHandler =
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
                    let! result = TradingStrategyService.saveAndSetCurrentStrategy agent dto
                    match result with
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
                    | Error (RepositoryError msg) -> return! ServerErrors.INTERNAL_ERROR msg next ctx
                | Error (InvalidInputError msg) -> return! RequestErrors.BAD_REQUEST msg next ctx
                | Error err -> return! ServerErrors.INTERNAL_ERROR (sprintf "%A" err) next ctx
            }

    // HTTP Handler for Getting Trading Strategy
    let getTradingStrategyHandler (agent: TradingStrategyAgent): HttpHandler =
        fun next ctx ->
            task {
                let logger = ctx.GetLogger()
                let! strategyOpt = TradingStrategyService.getCurrentStrategy agent
                match strategyOpt with
                | Some strategy ->
                    let dto = {
                        Id = BsonObjectId(ObjectId.GenerateNewId()) // Assuming a new ID for serialization
                        NumberOfCurrencies = strategy.NumberOfCurrencies
                        MinimalPriceSpread = strategy.MinimalPriceSpread
                        MinTransactionProfit = strategy.MinTransactionProfit
                        MaximalTransactionValue = strategy.MaximalTransactionValue
                        MaximalTradingValue = strategy.MaximalTradingValue
                        InitInvestment = strategy.InitInvestment
                    }
                    let jsonOptions = JsonSerializerOptions()
                    jsonOptions.Converters.Add(JsonFSharpConverter())
                    let jsonResponse = JsonSerializer.Serialize(dto, jsonOptions)
                    ctx.SetContentType "application/json"
                    return! text jsonResponse next ctx
                | None ->
                    return! RequestErrors.NOT_FOUND "No strategy defined yet" next ctx
            }

    // Web Application Composition with Explicit Type Annotation
    let createWebApp (agent: TradingStrategyAgent): HttpHandler =
        choose [
            POST >=> route "/trading-strategy" >=> updateTradingStrategyHandler agent
            GET >=> route "/trading-strategy" >=> getTradingStrategyHandler agent
        ]