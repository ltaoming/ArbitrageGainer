namespace ArbitrageGainer.Services

type Mongo_db = {
    db_name: string
    url: string
}

type Config = {
    mongo_db: Mongo_db
}

module Config =
    let private hardcodedMongoDb = {
        db_name = "0tt00t"
        url = "mongodb+srv://0tt00t:0tt00t@cluster0.e57xk.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0"  
    }

    let private hardcodedConfig = {
        mongo_db = hardcodedMongoDb
    }

    let get_config() = hardcodedConfig

    let get_mongo_config() = hardcodedConfig.mongo_db