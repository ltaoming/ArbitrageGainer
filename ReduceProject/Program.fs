module ArbitrageGainer.HistoryArbitrageOpportunity.Reduce

open System
open System.Text
open System.Collections.Generic
open FSharp.Data

type Dataset = JsonProvider<"""{
    "ev":"XQ",
    "pair":"FTM-USD",
    "lp":0,
    "ls":0,
    "bp":0.2463,
    "bs":26.55438555,
    "ap":0.2466,
    "as":8079.66184002,
    "t":1690409232184,
    "x":23,
    "r":1690409232227
}""">

[<EntryPoint>]
let main argv =

    let lines = 
        Seq.initInfinite (fun _ -> Console.ReadLine())
        |> Seq.takeWhile (fun line -> line <> null)
        |> Seq.toList

    let dict = Dictionary<int64, List<string>>()

    for line in lines do
        let parts = line.Split('\t')
        match parts.Length with
        | 2 ->
            let bucketStr = parts.[0]
            let jsonStr = parts.[1]
            match Int64.TryParse(bucketStr) with
            | true, bucket ->
                match dict.ContainsKey(bucket) with
                | false -> 
                    dict.Add(bucket, List<string>())
                    dict.[bucket].Add(jsonStr)
                | true -> 
                    dict.[bucket].Add(jsonStr)
            | _ ->
                //eprintfn "Failed to parse bucket from line: %s" line
        | _ ->
            //eprintfn "Invalid line format: %s" line

    let processBucket (jsonList: List<string>) =
        let records = 
            jsonList 
            |> Seq.choose (fun js -> try Some(Dataset.Parse(js)) with _ -> None)
            |> Seq.toArray

        let intermediate =
            records
            |> Seq.groupBy (fun item -> item.Pair)
            |> Seq.filter (fun (_, items) ->
                let distinctXCount = items |> Seq.map (fun i -> i.X) |> Seq.distinct |> Seq.length
                distinctXCount > 1
            )
            |> Seq.map (fun (pairKey, items) ->
                let byX = 
                    items 
                    |> Seq.groupBy (fun i -> i.X)
                    |> Seq.map (fun (xKey, groupX) ->
                        let maxPair = groupX |> Seq.maxBy (fun q -> q.Bp)
                        (xKey, maxPair)
                    )
                (pairKey, byX)
            )
            |> Seq.toArray

        intermediate
        |> Seq.map (fun (pairKey, dataSeq) ->
            let allCombos =
                dataSeq 
                |> Seq.collect (fun p1 ->
                    dataSeq
                    |> Seq.map (fun p2 -> (p1, p2)))

            let hasOpportunity =
                allCombos
                |> Seq.exists (fun ((_, r1), (_, r2)) ->
                    (r1.Bp - r2.Ap > 0.01M) || (r2.Bp - r1.Ap > 0.01M)
                )
            
            let count = 
                match hasOpportunity with
                | true -> 1
                | false -> 0
            (pairKey, count)
        )
        |> Seq.groupBy fst
        |> Seq.map (fun (pairName, vals) ->
            (pairName, vals |> Seq.sumBy snd)
        )
        |> Seq.filter (fun (_, v) -> v > 0)

    let finalResults =
        dict
        |> Seq.collect (fun kvp ->
            processBucket kvp.Value
        )
        |> Seq.groupBy fst
        |> Seq.map (fun (pairName, counts) ->
            (pairName, counts |> Seq.sumBy snd)
        )
        |> Seq.filter (fun (_, cnt) -> cnt > 0)
        |> Seq.map (fun (pairName, cnt) -> sprintf "%s, %d opportunities" pairName cnt)
        |> Seq.toArray

    for r in finalResults do
        //printfn "%s" r

    0
