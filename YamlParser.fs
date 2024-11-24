namespace ArbitrageGainer

open System
open System.IO
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.Collections.Generic

type Mongo_db = {
    db_name: string
    url: string
}

type Config = {
    mongo_db: Mongo_db
}

module YamlParser =
    let readYamlContent (content: string) =
        try
            let deserializer = DeserializerBuilder()
                                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                .Build()
            printfn "YAML Content:\n%s" content

            // Deserialize to a dictionary
            let dict = deserializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(content)
            printfn "Deserialized Dictionary: %A" dict

            // Manually map the dictionary to your type
            let mongoDbDict = dict.["mongo_db"]
            let mongo_db = {
                db_name = mongoDbDict.["db_name"]
                url = mongoDbDict.["url"]
            }
            let config = { mongo_db = mongo_db }
            printfn "Deserialized Config: %A" config
            Some config
        with
        | :? KeyNotFoundException as ex ->
            printfn "Key not found: %s" ex.Message
            None
        | :? YamlDotNet.Core.YamlException as ex ->
            printfn "YAML Exception: %s" ex.Message
            printfn "Stack Trace: %s" ex.StackTrace
            None
        | ex ->
            printfn "An error occurred while reading the configuration file: %s" ex.Message
            printfn "Stack Trace: %s" ex.StackTrace
            None

    let readYamlFile (filePath: string) =
        if File.Exists(filePath) then
            let content = File.ReadAllText(filePath)
            readYamlContent content
        else
            printfn "Configuration file not found: %s" filePath
            None