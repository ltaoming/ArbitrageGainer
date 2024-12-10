module ArbitrageGainer.HistoryArbitrageOpportunity.Map

open System
open System.Text.RegularExpressions
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

let extractJsonObjects (line: string) =
    let pattern = @"(\{[^}]+\})"
    let matches = Regex.Matches(line, pattern)
    [ for m in matches -> m.Value ]

let parseLine (line: string) =
    match String.IsNullOrWhiteSpace(line) with
    | true -> []
    | false ->
        let objects = extractJsonObjects line
        match objects.Length with
        | 0 ->
            //eprintfn "Failed to parse line (no JSON found): %s" line
            []
        | _ ->
            objects
            |> List.collect (fun jsonObj ->
                let parsed = try Some(Dataset.Parse(jsonObj)) with _ -> None
                match parsed with
                | Some record ->
                    //eprintfn "Parsed Record: %A" record
                    let bucket = record.T / 5L
                    [(bucket, jsonObj)]
                | None ->
                    //eprintfn "Failed to parse json object: %s" jsonObj
                    []
            )

[<EntryPoint>]
let main argv =
    let rec loop () =
        let line = Console.ReadLine()
        match line with
        | null -> ()
        | _ ->
            let results = parseLine line
            match results with
            | [] -> ()
            | _ ->
                results |> List.iter (fun (bucket, jsonStr) ->
                    //printfn "%d\t%s" bucket jsonStr
                )
            loop ()
    loop ()
    0
