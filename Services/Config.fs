module ArbitrageGainer.Services.Config

open System.IO
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

type Config = {
    mongo_db: Mongo_db
}
and Mongo_db = {
    db_name: string
    url: string
}

let readYamlFile (filePath: string) =
    let deserializer = DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build()
    let yamlContent = File.ReadAllText(filePath)
    deserializer.Deserialize<Config>(yamlContent)

let config = readYamlFile "config.yml"