module Service.CrossTradePair

open System.Text.Json

type CurrencyPair = { Currency1: string; Currency2: string }

type BitstampPairInfo = {
    name: string
}

type KrakenAssetPair = {
    wsname: string
}

type KrakenResponse = {
    error: string[]
    result: Map<string, KrakenAssetPair>
}

// Extract Bitfinex pairs
let extractBitfinexPairs (rawData: string) : seq<CurrencyPair> =
    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    match JsonSerializer.Deserialize<string[][]>(rawData, options) with
    | [| pairs |] ->
        pairs
        |> Seq.choose (fun s ->
            let s = s.Replace(":", "/")
            let parts = s.Split [| '/' |]
            match parts with
            | [| c1; c2 |] when c1.Length = 3 && c2.Length = 3 ->
                Some { Currency1 = c1; Currency2 = c2 }
            | _ -> None)
    | _ -> Seq.empty

// Extract Bitstamp pairs
let extractBitstampPairs (rawData: string) : seq<CurrencyPair> =
    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    match JsonSerializer.Deserialize<BitstampPairInfo[]>(rawData, options) with
    | pairs ->
        pairs
        |> Seq.choose (fun p ->
            let parts = p.name.Split [| '/' |]
            match parts with
            | [| c1; c2 |] when c1.Length = 3 && c2.Length = 3 ->
                Some { Currency1 = c1; Currency2 = c2 }
            | _ -> None)
    | _ -> Seq.empty

// Extract Kraken pairs
let extractKrakenPairs (rawData: string) : seq<CurrencyPair> =
    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    match JsonSerializer.Deserialize<KrakenResponse>(rawData, options) with
    | { error = [||]; result = data } ->
        data.Values
        |> Seq.choose (fun p ->
            let parts = p.wsname.Split [| '/' |]
            match parts with
            | [| c1; c2 |] when c1.Length = 3 && c2.Length = 3 ->
                Some { Currency1 = c1; Currency2 = c2 }
            | _ -> None)
    | _ -> Seq.empty

let identifyCrossTradedPairs (pairs: seq<CurrencyPair seq>) =
    pairs
    |> Seq.concat
    |> Seq.groupBy id
    |> Seq.filter (fun (_, group) -> Seq.length group > 1)
    |> Seq.map fst
