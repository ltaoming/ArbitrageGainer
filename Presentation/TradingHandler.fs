namespace Presentation.TradingHandler

open System
open System.Text.Json
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open MongoDB.Driver
open MongoDB.Bson
open ArbitrageGainer.Database
open ArbitrageGainer.Services.Repository.TradingStrategyRepository
open Application

type StartTradingRequest = {
    NumberOfPairs: int
}

module TradingHandler =
    let getCrossTradedPairsFromDB () =
        let collection : IMongoCollection<BsonDocument> = db.GetCollection<BsonDocument>("cross_traded_pairs")
        let filter = Builders<BsonDocument>.Filter.Empty
        use cursor = collection.FindSync(filter)
        // Explicitly assign the type of docs:
        let docs : System.Collections.Generic.List<BsonDocument> = cursor.ToList()
        docs
        |> Seq.map (fun (doc: BsonDocument) -> (doc.GetValue("pair")).AsString)
        |> Seq.toList

    let performHistoricalAnalysis() =
        let dataPath = "../../../historicalData.txt"
        let data = ArbitrageGainer.HistoryArbitrageOpportunity.loadData dataPath
        ArbitrageGainer.HistoryArbitrageOpportunity.calculateHistoryArbitrageOpportunity data
        |> Seq.toList

    let startTradingHandler (agent: TradingStrategyAgent): HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! startTradingRequest = ctx.BindJsonAsync<StartTradingRequest>()
                let numberOfPairs = startTradingRequest.NumberOfPairs

                // Perform historical analysis
                let historicalPairs = performHistoricalAnalysis()

                // Get cross-traded pairs from DB
                let crossTradedPairs = getCrossTradedPairsFromDB()

                // Determine which currency pairs to track
                let pairsToTrack =
                    historicalPairs
                    |> List.filter (fun pair -> List.contains pair crossTradedPairs)
                    |> List.truncate numberOfPairs

                printfn "Starting subscriptions for pairs: %A" pairsToTrack

                // Set the start date of trading now
                agent.SetStartDateOfTrading(DateTime.UtcNow)

                // Start the subscriptions (mock endpoint)
                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e"
                let testUri = Uri("wss://one8656-live-data.onrender.com/")
                let subscriptionParametersList =
                    pairsToTrack
                    |> List.map (fun pair -> "XQ." + pair)

                let connectionTasks =
                    subscriptionParametersList
                    |> List.map (fun paramStr ->
                        RealTimeMarketData.PolygonWebSocket.start (testUri, apiKey, paramStr)
                    )

                Async.Parallel connectionTasks
                |> Async.Ignore
                |> Async.Start

                return! json pairsToTrack next ctx
            }