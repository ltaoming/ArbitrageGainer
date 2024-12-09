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
        InitInvestment = InitialInvestment dto.InitInvestment
    }

    let fromDomain (strategy: TradingStrategy) (id: BsonObjectId) : TradingStrategyDto = {
        NumberOfCurrencies = strategy.NumberOfCurrencies
        MinimalPriceSpread = strategy.MinimalPriceSpread
        MinTransactionProfit = strategy.MinTransactionProfit
        MaximalTransactionValue = strategy.MaximalTransactionValue
        MaximalTradingValue = strategy.MaximalTradingValue
        InitInvestment = 
            match strategy.InitInvestment with
            | InitialInvestment v -> v
    }

type TradingStrategyDtoOption = TradingStrategyDto option
let collection = db.GetCollection<TradingStrategyDto>("trading_strategies")

/// Insert identified cross-traded currency pairs into the database for future reference.
/// This is called after we identify cross-traded pairs in the getCrossTradedPairsHandler.
let insertCrossTradedPairs (pairs: string[]) =
    let collection = db.GetCollection<BsonDocument>("cross_traded_pairs")
    let documents = pairs |> Array.map (fun pair -> BsonDocument("pair", BsonString(pair)))
    collection.InsertMany(documents)

/// Retrieve cross-traded pairs from the database
let getCrossTradedPairsFromDb () =
    let collection = db.GetCollection<BsonDocument>("cross_traded_pairs")
    let filter = Builders<BsonDocument>.Filter.Empty
    let cursor = collection.Find(filter).ToCursor()
    let result = 
        cursor.ToEnumerable()
        |> Seq.map (fun doc -> doc.GetValue("pair").AsString)
        |> Seq.toList
    result

let getTradingStrategy =
    try
        let collection = db.GetCollection<TradingStrategyDtoOption>("trading_strategies")
        let filter = Builders<TradingStrategyDtoOption>.Filter.Empty
        let strategyDto = collection.Find(filter).FirstOrDefault()
        // let strategyWithNull = isNull strategyDto
        match strategyDto with
        | None -> Error ("Trading startegy not found")
        | Some dto -> 
            let strategy = TradingStrategyConversion.toDomain dto
            Ok (strategy)
    with
    | ex -> Error (ex.Message)

let createTradingStrategy (tradingStrategy: TradingStrategy) =
    try
        let newTradingStrategy = { 
                                   NumberOfCurrencies = tradingStrategy.NumberOfCurrencies
                                   MinimalPriceSpread = tradingStrategy.MinimalPriceSpread
                                   MinTransactionProfit = tradingStrategy.MinTransactionProfit
                                   MaximalTransactionValue = tradingStrategy.MaximalTransactionValue
                                   MaximalTradingValue = tradingStrategy.MaximalTradingValue
                                   InitInvestment = 
                                   match tradingStrategy.InitInvestment with
                                    | InitialInvestment v -> v }
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

let testMongoDBConnection () =
    try
        let collection = db.GetCollection<BsonDocument>("testCollection")
        let count = collection.CountDocuments(FilterDefinition<BsonDocument>.Empty)
        printfn "Connected to MongoDB! Document count in 'testCollection': %d" count
        true
    with
    | ex ->
        printfn "Failed to connect to MongoDB: %s" ex.Message
        false

type ITradingStrategyRepository =
    abstract member Save : TradingStrategy -> Result<unit, TradingStrategyError>
    abstract member Load : unit -> Result<TradingStrategy option, TradingStrategyError>