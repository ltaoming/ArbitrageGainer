module ArbitrageGainer.Services.Repository.TransactionRepository

open Microsoft.FSharp.Core
open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Database
open System

type TransactionStatus =
    | Submitted of string
    | Fulfilled of string
    | PartiallyFulfilled of string
    | Cancelled of string

type Transaction = {
    TransactionId: string
    Status: string
    ListOfOrderIds: string list
    Timestamp: DateTime
}

type TransactionDto = {
    Id: BsonObjectId
    TransactionId: string
    Status: string
    ListOfOrderIds: string list
    Timestamp: DateTime
}

type TransactionHistoryDto = {
    Id: BsonObjectId
    TransactionId: string
    Status: string
    ListOfOrderIds: string list
    Timestamp: DateTime
    HistoryTimestamp: DateTime
}


let collection = db.GetCollection<TransactionDto>("transactions")

let getTransaction (transactionId: string):Result<Transaction, string> =
    
    try
        let filter = Builders<TransactionDto>.Filter.Eq((fun o -> o.TransactionId), transactionId)
        let res = collection.Find(filter).FirstOrDefault()
        let convertedRes = { TransactionId = res.TransactionId
                             Status = res.Status
                             ListOfOrderIds = res.ListOfOrderIds
                             Timestamp = res.Timestamp }
        Ok (convertedRes)
    with
    | ex -> Error (ex.Message)
    
    
let createTransaction (transaction: Transaction):Result<string, string> =
    try
        let newTransaction = { Id = BsonObjectId(ObjectId.GenerateNewId())
                               TransactionId = transaction.TransactionId
                               Status = transaction.Status
                               ListOfOrderIds = transaction.ListOfOrderIds
                               Timestamp = transaction.Timestamp }
        let res = collection.InsertOne(newTransaction)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)
    
let addOrderToTransaction (transactionId: string) (orderId: string):Result<string, string> =
    try
        let filter = Builders<TransactionDto>.Filter.Eq((fun o -> o.TransactionId), transactionId)
        let res = collection.Find(filter).FirstOrDefault()
        let updatedRes = { Id = res.Id
                           TransactionId = res.TransactionId
                           Status = res.Status
                           ListOfOrderIds = orderId :: res.ListOfOrderIds
                           Timestamp = res.Timestamp }
        let update = Builders<TransactionDto>.Update.Set((fun o -> o.ListOfOrderIds), updatedRes.ListOfOrderIds)
        let res = collection.UpdateOne(filter, update)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)

let getOrdersFromTransaction (transactionId: string):Result<string list, string> =
    try
        let filter = Builders<TransactionDto>.Filter.Eq((fun o -> o.TransactionId), transactionId)
        let res = collection.Find(filter).FirstOrDefault()
        Ok (res.ListOfOrderIds)
    with
    | ex -> Error (ex.Message)

let updateTransactionStatus (transactionId: string, newStatus: string): Result<string, string> =
    try
        let filter = Builders<TransactionDto>.Filter.Eq((fun o -> o.TransactionId), transactionId)
        let update = Builders<TransactionDto>.Update.Set((fun o -> o.Status), newStatus)
        let result = collection.UpdateOne(filter, update)
        match result.ModifiedCount with
        | count when count > 0L -> Ok "Transaction status updated successfully."
        | _ -> Error "No transaction found with the given TransactionId."
    with
    | ex -> Error ex.Message

let collectionHistory: IMongoCollection<TransactionHistoryDto> = db.GetCollection<TransactionHistoryDto>("transaction_history")

let toOption (value: 'T) : Option<'T> =
    match box value with
    | null -> None
    | _ -> Some value

let storeTransactionHistory (transactionId: string): Result<string, string> =
    try
        let filter = Builders<TransactionDto>.Filter.Eq((fun o -> o.TransactionId), transactionId)
        let transactionOption =
            collection.Find(filter).FirstOrDefault() |> toOption
        
        match transactionOption with
        | None -> Error "Transaction not found."
        | Some transaction ->
            let historyRecord = { 
                Id = BsonObjectId(ObjectId.GenerateNewId())
                TransactionId = transaction.TransactionId
                Status = transaction.Status
                ListOfOrderIds = transaction.ListOfOrderIds
                Timestamp = transaction.Timestamp
                HistoryTimestamp = DateTime.UtcNow
            }
            collectionHistory.InsertOne(historyRecord)
            Ok "Transaction history stored successfully."
    with
    | ex -> Error ex.Message

