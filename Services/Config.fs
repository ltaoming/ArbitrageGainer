namespace ArbitrageGainer.Services


type Mongo_db = {
    db_name: string
    url: string
}

type Config = {
    mongo_db: Mongo_db
}

module Config =
    open DotNetEnv
    open System
    Env.Load()
    let private hardcodedMongoDb = 
        {
            db_name = Env.GetString("MONGO_DB_NAME")
            url = Env.GetString("MONGO_DB_URL")
            
        }

    let private hardcodedConfig = {
        mongo_db = hardcodedMongoDb
    }

    let get_config() = hardcodedConfig

    let get_mongo_config() = hardcodedConfig.mongo_db