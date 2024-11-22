module ArbitrageGainer.Services.Repository.TradingStrategyRepository

open Domain
open Microsoft.FSharp.Core
open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Database

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

type TradingStrategy = {
    Id : BsonObjectId
    NumberOfCurrencies: CurrencyCount
    MinimalPriceSpread: PriceSpread
    MinTransactionProfit: TransactionValue
    MaximalTransactionValue: TransactionValue
    MaximalTradingValue: TradingValue
    InitInvestment: float
}

let collection = db.GetCollection<TradingStrategy>("trading_strategies")

let getTradingStrategy (tradingStrategyId: BsonObjectId) =
    try
        let filter = Builders<TradingStrategy>.Filter.Eq((fun ts -> ts.Id), tradingStrategyId)
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
        let filter = Builders<TradingStrategy>.Filter.Empty
        let update = Builders<TradingStrategy>.Update.Set((fun ts -> ts.MaximalTradingValue), maximalTradingValue)
        let res = collection.UpdateMany(filter, update)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)