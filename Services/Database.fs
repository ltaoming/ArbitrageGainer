module ArbitrageGainer.Database

open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Services.Config
open Domain
open ArbitrageGainer.Core


type CrossTradedPair = {
    Pair: string
}

let connString = config.mongo_db.url
let dbName = config.mongo_db.db_name
let client = new MongoClient(connString)
let db = client.GetDatabase(dbName)

let getCollection<'T> (collectionName: string) =
    db.GetCollection<'T>(collectionName)

let insertDocument<'T> (collectionName: string) (document: 'T) =
    let collection = getCollection<'T> collectionName
    collection.InsertOne(document)
let getDocuments<'T> (collectionName: string) =
    let collection = getCollection<'T> collectionName
    collection.Find(fun x -> true).ToList()
let getTradingStrategies() =
    getDocuments<TradingStrategy> "TradingStrategies"

let insertCrossTradedPairs (pairs: seq<string>) =
    let collection = getCollection<CrossTradedPair>("CrossTradedPairs")
    let documents = pairs |> Seq.map (fun pair -> { Pair = pair })
    collection.InsertMany(documents)
// let getHistoricalArbitrageOpportunities() =
//     getDocuments<HistoricalArbitrageOpportunities> "HistoricalArbitrageOpportunities"

// let getRealTimeTradingOrders() =
//     getDocuments<Order> "RealTimeTradingOrders"

// let getTradingHistory() =
//     getDocuments<TradeRecord> "TradingHistory"

// let getPNLData() =
//     getDocuments<PNLRecord> "PNLData"