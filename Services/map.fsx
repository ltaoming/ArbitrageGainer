module ArbitrageGainer.HistoryArbitrageOpportunity.Map

open System
open FSharp.Data

#r "nuget: FSharp.Data" 
type Dataset = JsonProvider<"""[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2463,"bs":26.55438555,"ap":0.2466,"as":8079.66184002,"t":1690409232184,"x":23,"r":1690409232227}]""">

/// Parses a single line of JSON data and generates key-value pairs (bucket, JSON string)
let parseLine (line: string) =
    match String.IsNullOrWhiteSpace(line) with
    | true -> None 
    | false ->
        match (try Some (Dataset.Parse(line)) with | _ -> None) with
        | Some record ->
            printfn "Parsed Record: %A" record
            // Ensure the field name matches
            let bucket = record.T / 5L
            Some (bucket, line)
        | None -> None

[<EntryPoint>]
let main argv =
    // Recursively read each line from standard input
    let rec loop () =
        let line = Console.ReadLine()
        match line with
        | null -> () // EOF, end loop
        | _ ->
            match parseLine line with
            | Some (bucket, jsonStr) ->
                // Print key-value pairs separated by tab
                printfn "%d\t%s" bucket jsonStr
            | None -> ()
            loop ()
    loop ()
    0 // Exit code