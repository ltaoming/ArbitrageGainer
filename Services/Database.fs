module ArbitrageGainer.Database

open MongoDB.Driver
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open ArbitrageGainer.Services.Config
let connString = config.mongo_db.url
let dbName = config.mongo_db.db_name
let client = new MongoClient(connString)
let db = client.GetDatabase(dbName)