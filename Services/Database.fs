namespace ArbitrageGainer

module Database =
    open MongoDB.Driver
    open ArbitrageGainer.Services.Config
    open Domain
    open ArbitrageGainer.Core

    type CrossTradedPair = {
        Pair: string
    }

    let connString = get_config().mongo_db.url
    let dbName = get_config().mongo_db.db_name
    let client = new MongoClient(connString)
    let db = client.GetDatabase(dbName)
    let getCollection<'T> (collectionName: string) =
        db.GetCollection<'T>(collectionName)