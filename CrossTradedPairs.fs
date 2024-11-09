module CrossTradedPairs

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

type CurrencyPair = { Currency1: string; Currency2: string }

type BitstampPairInfo = {
    [<JsonPropertyName("name")>]
    name: string
}

type KrakenAssetPair = {
    [<JsonPropertyName("wsname")>]
    wsname: string
}

type KrakenResponse = {
    [<JsonPropertyName("error")>]
    error: string[]
    [<JsonPropertyName("result")>]
    result: System.Collections.Generic.Dictionary<string, KrakenAssetPair>
}

let httpClient = new HttpClient()

let fetchBitfinexPairs () : Async<Result<Set<CurrencyPair>, string>> = async {
    let url = "https://api-pub.bitfinex.com/v2/conf/pub:list:pair:exchange"
    try
        let! response = httpClient.GetAsync(url) |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        let options = JsonSerializerOptions()
        options.PropertyNameCaseInsensitive <- true
        let pairsArrays = JsonSerializer.Deserialize<string[][]>(content, options)
        match pairsArrays with
        | [| pairs |] ->
            let currencyPairs =
                pairs
                |> Seq.choose (fun s ->
                    let s = s.Replace(":", "/")
                    let parts = s.Split [| '/' |]
                    if parts.Length = 2 then
                        let c1 = parts.[0]
                        let c2 = parts.[1]
                        if c1.Length = 3 && c2.Length = 3 then
                            Some { Currency1 = c1; Currency2 = c2 }
                        else
                            None
                    else
                        None)
                |> Set.ofSeq
            return Ok currencyPairs
        | _ ->
            return Error "Unexpected JSON format from Bitfinex"
    with
    | ex ->
        return Error ex.Message
}

let fetchBitstampPairs () : Async<Result<Set<CurrencyPair>, string>> = async {
    let url = "https://www.bitstamp.net/api/v2/trading-pairs-info/"
    try
        let! response = httpClient.GetAsync(url) |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        let options = JsonSerializerOptions()
        options.PropertyNameCaseInsensitive <- true
        let pairsInfo = JsonSerializer.Deserialize<BitstampPairInfo[]>(content, options)
        match pairsInfo with
        | null -> return Error "Failed to parse Bitstamp response"
        | _ ->
            let currencyPairs =
                pairsInfo
                |> Seq.choose (fun p ->
                    let s = p.name
                    let parts = s.Split [| '/' |]
                    if parts.Length = 2 then
                        let c1 = parts.[0]
                        let c2 = parts.[1]
                        if c1.Length = 3 && c2.Length = 3 then
                            Some { Currency1 = c1; Currency2 = c2 }
                        else
                            None
                    else
                        None)
                |> Set.ofSeq
            return Ok currencyPairs
    with
    | ex ->
        return Error ex.Message
}

let fetchKrakenPairs () : Async<Result<Set<CurrencyPair>, string>> = async {
    let url = "https://api.kraken.com/0/public/AssetPairs"
    try
        let! response = httpClient.GetAsync(url) |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        let options = JsonSerializerOptions()
        options.PropertyNameCaseInsensitive <- true

        // Wrap deserialization in a try...with block
        let krakenResponse =
            try
                JsonSerializer.Deserialize<KrakenResponse>(content, options)
            with ex ->
                // Handle deserialization exceptions
                raise (Exception("Failed to parse Kraken response", ex))

        if krakenResponse.error.Length > 0 then
            return Error (String.concat ", " krakenResponse.error)
        else
            let currencyPairs =
                krakenResponse.result.Values
                |> Seq.choose (fun p ->
                    let s = p.wsname
                    if String.IsNullOrEmpty(s) then None
                    else
                        let parts = s.Split [| '/' |]
                        if parts.Length = 2 then
                            let c1 = parts.[0]
                            let c2 = parts.[1]
                            if c1.Length = 3 && c2.Length = 3 then
                                Some { Currency1 = c1; Currency2 = c2 }
                            else
                                None
                        else
                            None)
                |> Set.ofSeq
            return Ok currencyPairs
    with
    | ex -> return Error ex.Message
}

let getCrossTradedPairs () : Async<Result<Set<CurrencyPair>, string>> = async {
    let! bitfinexResult = fetchBitfinexPairs()
    let! bitstampResult = fetchBitstampPairs()
    let! krakenResult = fetchKrakenPairs()
    match bitfinexResult, bitstampResult, krakenResult with
    | Ok bitfinexPairs, Ok bitstampPairs, Ok krakenPairs ->
        let allPairs = [ bitfinexPairs; bitstampPairs; krakenPairs ]
        let pairCounts =
            allPairs
            |> List.collect Set.toList
            |> List.groupBy id
            |> List.map (fun (pair, occurrences) -> (pair, List.length occurrences))
            |> Map.ofList
        let crossTradedPairs =
            pairCounts
            |> Map.filter (fun _ count -> count >= 2)
            |> Map.keys 
            |> Set.ofSeq
        return Ok crossTradedPairs
    | _ ->
        let errors =
            [ bitfinexResult; bitstampResult; krakenResult ]
            |> List.choose (function
                | Error msg -> Some msg
                | _ -> None)
        return Error (String.concat "; " errors)
}

let crossTradedPairsFilePath = "cross_traded_pairs.json"

let saveCrossTradedPairsToFile (pairs: Set<CurrencyPair>) =
    let formattedPairs =
        pairs
        |> Seq.map (fun p -> $"{p.Currency1}-{p.Currency2}")
        |> Seq.toArray
    let json = JsonSerializer.Serialize(formattedPairs)
    System.IO.File.WriteAllText(crossTradedPairsFilePath, json)

let getCrossTradedPairsHandler : HttpHandler =
    fun next ctx ->
        task {
            let! result = getCrossTradedPairs ()
            match result with
            | Ok pairs ->
                saveCrossTradedPairsToFile pairs
                let formattedPairs =
                    pairs
                    |> Seq.map (fun p -> $"{p.Currency1}-{p.Currency2}")
                    |> Seq.toArray
                return! json formattedPairs next ctx
            | Error errMsg ->
                return! RequestErrors.BAD_REQUEST errMsg next ctx
        }
