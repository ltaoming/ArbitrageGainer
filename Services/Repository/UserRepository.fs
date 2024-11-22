module ArbitrageGainer.Services.Repository.UsersRepository

open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Database

type User = {
    Id : BsonObjectId
    username: string
    email: string
    strategyId: BsonObjectId
}

let collection = db.GetCollection<User>("users")

let getUser (username: string) =
    try
        let filter = Builders<User>.Filter.Eq((fun u -> u.username), username)
        let res = collection.Find(filter).FirstOrDefault()
        Ok (res)
    with
    | ex -> Error (ex.Message)
    
let createUser (user: User) =
    try
        let newUser = { Id = BsonObjectId(ObjectId.GenerateNewId()); username = user.username; email = user.email; strategyId = user.strategyId }
        let res = collection.InsertOne(newUser)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)
    
let saveEmail ((username: string), (email: string)) =
    try
        let filter = Builders<User>.Filter.Eq((fun u -> u.username), username)
        let update = Builders<User>.Update.Set((fun u -> u.email), email)
        let res = collection.UpdateOne(filter, update)
        Ok ("Success")
    with
    | ex -> Error (ex.Message)
