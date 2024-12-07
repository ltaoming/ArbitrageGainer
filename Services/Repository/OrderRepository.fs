module ArbitrageGainer.Services.Repository.OrderRepository

open Domain
open Microsoft.FSharp.Core
open MongoDB.Driver
open MongoDB.Bson
open ArbitrageGainer.Database
open System

type Order = {
    OrderId: string
    CurrencyPair: string
    Type: string  // "buy" or "sell"
    OrderQuantity: decimal
    FilledQuantity: decimal
    OrderPrice: decimal
    Exchange: string
    Status: string // "FullyFilled", "PartiallyFulfilled", etc.
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

let getOrder (orderId: string): Result<Order, string> =
    try
        let filter = Builders<OrderDto>.Filter.Eq((fun o -> o.OrderId), orderId)
        let res = collection.Find(filter).FirstOrDefault()
        match box res with
        | null -> Error "Order not found"
        | _ ->
            let ret = { 
                OrderId = res.OrderId
                CurrencyPair = res.CurrencyPair
                Type = res.Type
                OrderQuantity = res.OrderQuantity
                FilledQuantity = res.FilledQuantity
                OrderPrice = res.OrderPrice
                Exchange = res.Exchange
                Status = res.Status
                TransactionId = res.TransactionId
                Timestamp = res.Timestamp
            }
            Ok ret
    with
    | ex -> Error (ex.Message)

let createOrder (order: Order): Result<string, string> =
    try
        let newOrder = {
            Id = BsonObjectId(ObjectId.GenerateNewId())
            OrderId = order.OrderId
            CurrencyPair = order.CurrencyPair
            Type = order.Type
            OrderQuantity = order.OrderQuantity
            FilledQuantity = order.FilledQuantity
            OrderPrice = order.OrderPrice
            Exchange = order.Exchange
            Status = order.Status
            TransactionId = order.TransactionId
            Timestamp = order.Timestamp
        }
        collection.InsertOne(newOrder)
        Ok "Success"
    with
    | ex -> Error (ex.Message)

let updateOrderStatus (order: Order): Result<string, string> =
    try
        let filter = Builders<OrderDto>.Filter.Eq((fun o -> o.OrderId), order.OrderId)
        let update =
            Builders<OrderDto>.Update
                .Set((fun o -> o.FilledQuantity), order.FilledQuantity)
                .Set((fun o -> o.Status), order.Status)
        let res = collection.UpdateOne(filter, update)
        match res.MatchedCount > 0L with
        | true -> Ok "Success"
        | false -> Error "No order updated"
    with
    | ex -> Error (ex.Message)

/// <summary>
/// Retrieves all fully filled orders within the given date range. 
/// Fully filled orders represent executed trades.
/// </summary>
let getOrdersInPeriod (startDate: DateTime) (endDate: DateTime) : Result<Order list, string> =
    try
        let filter = 
            Builders<OrderDto>.Filter.And(
                Builders<OrderDto>.Filter.Gte((fun o -> o.Timestamp), startDate),
                Builders<OrderDto>.Filter.Lte((fun o -> o.Timestamp), endDate),
                Builders<OrderDto>.Filter.Eq((fun o -> o.Status), "FullyFilled")
            )
        let orders = 
            collection.Find(filter).ToList() 
            |> Seq.map (fun res -> 
                {
                    OrderId = res.OrderId
                    CurrencyPair = res.CurrencyPair
                    Type = res.Type
                    OrderQuantity = res.OrderQuantity
                    FilledQuantity = res.FilledQuantity
                    OrderPrice = res.OrderPrice
                    Exchange = res.Exchange
                    Status = res.Status
                    TransactionId = res.TransactionId
                    Timestamp = res.Timestamp
                })
            |> Seq.toList
        Ok orders
    with ex ->
        Error ex.Message
