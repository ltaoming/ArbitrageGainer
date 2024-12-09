module ArbitrageGainer.HistoryArbitrageOpportunity

open System.IO
open FSharp.Data
open Logging.Logger 

type Dataset = JsonProvider<"""[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2463,"bs":26.55438555,"ap":0.2466,"as":8079.66184002,"t":1690409232184,"x":23,"r":1690409232227}]""">

let loadData () =
    let baseDir = System.AppContext.BaseDirectory
    let filePath = Path.Combine(baseDir, "historicalData.txt")
    Dataset.Load filePath

let parseData (jsonStr:string) =
    Dataset.Parse jsonStr

let prepareData (rootData: Dataset.Root array)= 
    rootData
    |> Seq.groupBy (fun (item: Dataset.Root) -> item.T / 5L)

// within the same bucket
let map (data: seq<Dataset.Root>) =
    // printfn "\nMap: %A" data
    data
    |> Seq.groupBy (fun (item:Dataset.Root) -> item.Pair)
    |> Seq.filter (fun (_, items) -> items |> Seq.map (fun item -> item.X) |> Seq.length > 1)
    |> Seq.map (fun (key, pair) -> 
        let mapResult =
            pair 
            |> Seq.groupBy (fun (item:Dataset.Root) -> item.X)
            |> Seq.map (fun (xKey, pair2) ->
                let maxPair =
                    pair2 |> Seq.maxBy (fun q -> q.Bp)
                (xKey, maxPair))
        (key, mapResult))

let reduce data =
    // printfn "\nReduce: %A" data
    data
    |> Seq.map (fun (key, pair) ->
        let allPairs = 
            pair 
            |> Seq.collect (fun pair1 -> 
                pair
                |> Seq.map (fun pair2 -> (pair1, pair2)))
        let getIntFromBool = function
            | true -> 1
            | _ -> 0
        let pairResult = 
            allPairs
            |> Seq.map (fun ((key1, (pair1:Dataset.Root)), (key2, (pair2:Dataset.Root))) ->
                ((pair1.Bp - pair2.Ap) > 0.01M) || ((pair2.Bp - pair1.Ap) > 0.01M))
            |> Seq.reduce (fun x y -> x || y)
            |> getIntFromBool
        (key, pairResult))
    |> Seq.groupBy fst 
    |> Seq.map (fun (x1, x2) ->
        (x1, x2 |> Seq.sumBy snd))


let filterZeroVal pair = 
    match pair with
    | (_, 0) -> false
    | (_, _) -> true
    
let getString pair:string =
    match pair with
    | (pairName, cnt) -> sprintf "%s, %d opportunities" pairName cnt
    | _ -> "error"
    
let getResults seqPair =
    seqPair
    |> Seq.filter filterZeroVal
    |> Seq.map getString
        

let logger = createLogger

let calculateHistoryArbitrageOpportunity (data: Dataset.Root array) =
    async {
        let preparedData = data |> prepareData

        let asyncMappedBuckets =
            preparedData
            |> Seq.map (fun (_, bucket) -> async { return map bucket })

        let! mappedResults = Async.Parallel asyncMappedBuckets

        let reducedResult = mappedResults |> Seq.concat |> reduce

        return getResults reducedResult
    }
    |> Async.RunSynchronously
// let runOnFile =
//     filePath |> loadData |> calculateHistoryArbitrageOpportunity |> Seq.iter (printfn "%A")