module ArbitrageGainer.Services.Repository.OrderRepository

open Domain
open Microsoft.FSharp.Core
open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Database
open System

// Validation Error Types
// type ValidationError =
//     | MissingField of string
//     | NumberOfCurrenciesMustBePositive
//     | MinimalPriceSpreadMustBePositive
//     | MaximalTransactionValueMustBePositive
//     | MaximalTradingValueMustBePositive
//     | MaximalTransactionValueLessThanMinimalPriceSpread
    

type Order = {
    OrderId: string
    CurrencyPair: string
    Type: string
    OrderQuantity: decimal
    FilledQuantity: decimal
    OrderPrice: decimal
    Exchange: string
    Status: string
    TransactionId: string
    Timestamp: DateTime
}

type OrderDto = {
    Id: BsonObjectId
    OrderId: string
    CurrencyPair: string
    Type: string
    OrderQuantity: decimal
    FilledQuantity: decimal
    OrderPrice: decimal
    Exchange: string
    Status: string
    TransactionId: string
    Timestamp: DateTime
}

let collection = db.GetCollection<OrderDto>("orders")

let getOrder (orderId: string):Result<Order, string> =
    try
        let filter = Builders<OrderDto>.Filter.Eq((fun o -> o.OrderId), orderId)
        let res = collection.Find(filter).FirstOrDefault()
        let ret = { OrderId = res.OrderId
                    CurrencyPair = res.CurrencyPair
                    Type = res.Type
                    OrderQuantity = res.OrderQuantity
                    FilledQuantity = res.FilledQuantity
                    OrderPrice = res.OrderPrice
                    Exchange = res.Exchange
                    Status = res.Status
                    TransactionId = res.TransactionId
                    Timestamp = res.Timestamp }
        Ok (ret)
    with
    | ex -> Error (ex.Message)
    
let createOrder (order: Order):Result<string, string> =
    try
        let newOrder = { Id = BsonObjectId(ObjectId.GenerateNewId())
                         OrderId = order.OrderId
                         CurrencyPair = order.OrderId
                         Type = order.Type
                         OrderQuantity = order.OrderQuantity
                         OrderPrice = order.OrderPrice
                         Exchange = order.Exchange
                         Status = order.Status
                         TransactionId = order.TransactionId
                         FilledQuantity = order.FilledQuantity
                         Timestamp = order.Timestamp}
        let res = collection.InsertOne(newOrder)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)

let updateOrderStatus (orderId: string, status: string, filledQuantity: decimal):Result<string, string> =
    try
        let filter = Builders<OrderDto>.Filter.Eq((fun o -> o.OrderId), orderId)
        let update = Builders<OrderDto>.Update
                        .Set((fun o -> o.Status), status)
                        .Set((fun o -> o.FilledQuantity), filledQuantity)
        let res = collection.UpdateOne(filter, update)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)