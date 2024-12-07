module Presentation.CrossTradePairHandler

open System
open Giraffe
open Service.CrossTradePair
open Infrastructure.CrossTradePairApi
open FSharp.Control.Tasks
open ArbitrageGainer.Services.Repository.TradingStrategyRepository  // Ensure this is included
open ArbitrageGainer.Logging.IdentificationLogger

let getCrossTradedPairsHandler : HttpHandler =
    fun next ctx ->
        task {
            // Start timing the operation
            let startTime = DateTime.UtcNow
            IdentificationLogger "getCrossTradedPairsHandler started"

            // Associate exchange names with their URLs
            let exchangeUrls = [
                ("bitfinex", "https://api-pub.bitfinex.com/v2/conf/pub:list:pair:exchange")
                ("bitstamp", "https://www.bitstamp.net/api/v2/trading-pairs-info/")
                ("kraken", "https://api.kraken.com/0/public/AssetPairs")
            ]

            IdentificationLogger "Fetching exchange data..."
            
            // Fetch data along with exchange names
            let fetchTasks = exchangeUrls |> List.map (fun (name, url) -> async {
                let! result = fetchExchangeData url
                return (name, result)
            })

            let! results = fetchTasks |> Async.Parallel

            IdentificationLogger "Data fetch completed. Checking for errors..."

            // Handle errors appropriately
            match results |> Array.tryFind (fun (_, res) -> Result.isError res) with
            | Some (_, Error errMsg) ->
                IdentificationLogger (sprintf "Error encountered: %s" errMsg)
                return! RequestErrors.BAD_REQUEST errMsg next ctx
            | _ ->
                IdentificationLogger "No errors found. Parsing data..."

                // Process the data with correct parsers
                let parsedData =
                    results
                    |> Array.choose (function
                        | ("bitfinex", Ok raw) -> Some (extractBitfinexPairs raw)
                        | ("bitstamp", Ok raw) -> Some (extractBitstampPairs raw)
                        | ("kraken", Ok raw) -> Some (extractKrakenPairs raw)
                        | _ -> None)

                IdentificationLogger "Data parsed successfully. Identifying cross-traded pairs..."

                let crossTradedPairs = identifyCrossTradedPairs parsedData
                let formattedPairs =
                    crossTradedPairs
                    |> Seq.map (fun p -> $"{p.Currency1}-{p.Currency2}")
                    |> Seq.toArray

                IdentificationLogger "Inserting cross-traded pairs into repository..."
                insertCrossTradedPairs formattedPairs

                // End timing the operation
                let endTime = DateTime.UtcNow
                let elapsed = endTime - startTime
                IdentificationLogger (sprintf "getCrossTradedPairsHandler completed in %f ms" elapsed.TotalMilliseconds)

                return! json formattedPairs next ctx
        }
