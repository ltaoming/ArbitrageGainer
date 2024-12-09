// reduce.fsx
module ArbitrageGainer.HistoryArbitrageOpportunity.Reduce

open System
open FSharp.Data

#r "nuget: FSharp.Data"


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

let processBucket (records: ResizeArray<Dataset.Root>) =
    records
    |> Seq.groupBy (fun r -> r.Pair)
    |> Seq.iter (fun (pair, items) ->
        let itemsArr = items |> Seq.toArray
        let distinctXCount = itemsArr |> Seq.map (fun i -> i.X) |> Seq.distinct |> Seq.length
        match distinctXCount > 1 with
        | true ->
            let groupedByX = 
                itemsArr
                |> Seq.groupBy (fun i -> i.X)

            let maxBp =
                groupedByX
                |> Seq.map (fun (_, grp) -> grp |> Seq.maxBy (fun r -> r.Bp))
                |> Seq.maxBy (fun r -> r.Bp)

            let maxAp =
                groupedByX
                |> Seq.map (fun (_, grp) -> grp |> Seq.maxBy (fun r -> r.Ap))
                |> Seq.maxBy (fun r -> r.Ap)

            let opportunity = (maxBp.Bp - maxAp.Ap) > 0.01M || (maxAp.Bp - maxBp.Ap) > 0.01M
            match opportunity with
            | true -> printfn "%s\t1" pair
            | false -> ()
        | false -> ()
    )

[<EntryPoint>]
let main argv =
    let mutable currentKey = ""
    let mutable records = ResizeArray<Dataset.Root>()

    let flushBucket () =
        match records.Count > 0 with
        | true ->
            processBucket records
            records.Clear()
        | false -> ()

    let rec loop () =
        let line = Console.ReadLine()
        match line with
        | null ->
            flushBucket()
        | _ ->
            let parts = line.Split('\t')
            match parts.Length with
            | 2 ->
                let key = parts.[0]
                let jsonStr = parts.[1]
                match key <> currentKey && currentKey <> "" with
                | true ->
                    flushBucket()
                    currentKey <- key
                | false ->
                    match currentKey = "" with
                    | true -> currentKey <- key
                    | false -> ()

                let parsed = (try Some(Dataset.Parse(jsonStr)) with _ -> None)
                match parsed with
                | Some record ->
                    records.Add(record)
                | None ->
                    eprintfn "Failed to parse json in reduce: %s" jsonStr
                loop ()
            | _ ->
                eprintfn "Malformed line: %s" line
                loop ()

    loop ()
    0
