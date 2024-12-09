// map.fsx
module ArbitrageGainer.HistoryArbitrageOpportunity.Map

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

let parseLine (line: string) =
    match String.IsNullOrWhiteSpace(line) with
    | true -> None 
    | false ->
        match (try Some (Dataset.Parse(line)) with | _ -> None) with
        | Some record ->
            eprintfn "Parsed Record: %A" record
            let bucket = record.T / 5L
            Some (bucket, line)
        | None -> 
            eprintfn "Failed to parse line: %s" line
            None

[<EntryPoint>]
let main argv =
    let rec loop () =
        let line = Console.ReadLine()
        match line with
        | null -> () 
        | _ ->
            match parseLine line with
            | Some (bucket, jsonStr) ->
                printfn "%d\t%s" bucket jsonStr
            | None -> ()
            loop ()
    loop ()
    0 
