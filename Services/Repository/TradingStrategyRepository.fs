module ArbitrageGainer.Services.Repository.TradingStrategyRepository

open ArbitrageGainer.Core
open Microsoft.FSharp.Core
open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Database
open System

module TradingStrategyConversion =

    let toDomain (dto: TradingStrategyDto) : TradingStrategy = {
        NumberOfCurrencies = dto.NumberOfCurrencies
        MinimalPriceSpread = dto.MinimalPriceSpread
        MinTransactionProfit = dto.MinTransactionProfit
        MaximalTransactionValue = dto.MaximalTransactionValue
        MaximalTradingValue = dto.MaximalTradingValue
        InitInvestment = dto.InitInvestment
    }

    let fromDomain (strategy: TradingStrategy) (id: BsonObjectId) : TradingStrategyDto = {
        Id = id
        NumberOfCurrencies = strategy.NumberOfCurrencies
        MinimalPriceSpread = strategy.MinimalPriceSpread
        MinTransactionProfit = strategy.MinTransactionProfit
        MaximalTransactionValue = strategy.MaximalTransactionValue
        MaximalTradingValue = strategy.MaximalTradingValue
        InitInvestment = strategy.InitInvestment
    }

let collection = db.GetCollection<TradingStrategyDto>("trading_strategies")

let getTradingStrategy (tradingStrategyId: BsonObjectId) =
    try
        let filter = Builders<TradingStrategyDto>.Filter.Eq((fun ts -> ts.Id), tradingStrategyId)
        let res = collection.Find(filter).FirstOrDefault()
        Ok (res)
    with
    | ex -> Error (ex.Message)
    
let createTradingStrategy (tradingStrategy: TradingStrategy) =
    try
        let newTradingStrategy = { Id = BsonObjectId(ObjectId.GenerateNewId())
                                   NumberOfCurrencies = tradingStrategy.NumberOfCurrencies
                                   MinimalPriceSpread = tradingStrategy.MinimalPriceSpread
                                   MinTransactionProfit = tradingStrategy.MinTransactionProfit
                                   MaximalTransactionValue = tradingStrategy.MaximalTransactionValue
                                   MaximalTradingValue = tradingStrategy.MaximalTradingValue
                                   InitInvestment = tradingStrategy.InitInvestment }
        let res = collection.InsertOne(newTradingStrategy)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)
    
let updateMaximalTradingValue (maximalTradingValue: TradingValue) =
    try
        let filter = Builders<TradingStrategyDto>.Filter.Empty
        let update = Builders<TradingStrategyDto>.Update.Set((fun ts -> ts.MaximalTradingValue), maximalTradingValue)
        let res = collection.UpdateMany(filter, update)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)

type ITradingStrategyRepository =
    abstract member Save : TradingStrategy -> Result<unit, TradingStrategyError>
    abstract member Load : unit -> Result<TradingStrategy option, TradingStrategyError>