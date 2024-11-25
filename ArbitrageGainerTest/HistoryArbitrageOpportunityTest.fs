module ArbitrageGainerTest.HistoryArbitrageOpportunityTest

open System
open NUnit.Framework
open FsUnit
open FSharp.Data
open ArbitrageGainer.HistoryArbitrageOpportunity

// Define the Dataset type with 'askSize' instead of 'as'
type Dataset = {
    ev: string
    pair: string
    lp: float
    ls: float
    bp: float
    bs: float
    ap: float
    askSize: float
    t: int64
    x: int
    r: int64
}

// Adjust the parseData function to return a list of Dataset
let parseData (jsonData: string) : Dataset list =
    let data = JsonValue.Parse(jsonData)
    data.AsArray()
    |> Array.map (fun item ->
        {
            ev = item.["ev"].AsString()
            pair = item.["pair"].AsString()
            lp = item.["lp"].AsFloat()
            ls = item.["ls"].AsFloat()
            bp = item.["bp"].AsFloat()
            bs = item.["bs"].AsFloat()
            ap = item.["ap"].AsFloat()
            askSize = item.["as"].AsFloat()
            t = item.["t"].AsInteger64()
            x = item.["x"].AsInteger()
            r = item.["r"].AsInteger64()
        }
    )
    |> Array.toList

// The map function from your module
let map (data: Dataset list) =
    data
    |> List.groupBy (fun d -> d.pair)
    |> List.map (fun (pair, datasets) ->
        pair,
        datasets
        |> List.map (fun d -> d.x, d)
    )

// Function to compare two Dataset records with floating-point tolerance
let compareDatasets (d1: Dataset) (d2: Dataset) =
    d1.ev = d2.ev &&
    d1.pair = d2.pair &&
    abs(d1.lp - d2.lp) < 0.0001 &&
    abs(d1.ls - d2.ls) < 0.0001 &&
    abs(d1.bp - d2.bp) < 0.0001 &&
    abs(d1.bs - d2.bs) < 0.0001 &&
    abs(d1.ap - d2.ap) < 0.0001 &&
    abs(d1.askSize - d2.askSize) < 0.0001 &&
    d1.t = d2.t &&
    d1.x = d2.x &&
    d1.r = d2.r

// Function to compare exchange ID and Dataset tuple
let compareEntries (e1: int * Dataset) (e2: int * Dataset) =
    let (ex1, d1) = e1
    let (ex2, d2) = e2
    ex1 = ex2 && compareDatasets d1 d2

// Function to compare the overall results
let compareResults (res1: (string * (int * Dataset) list) list) (res2: (string * (int * Dataset) list) list) =
    List.length res1 = List.length res2 &&
    List.forall2 (fun (pair1, data1) (pair2, data2) ->
        pair1 = pair2 &&
        List.length data1 = List.length data2 &&
        List.forall2 compareEntries data1 data2
    ) res1 res2

[<SetUp>]
let Setup () =
    ()

[<Test>]
let ``map function should group and filter correctly`` () =
    let data = 
        """[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2576,"bs":26.55438555,"ap":0.2463,"as":8079.66184002,"t":1690409232182,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2492,"bs":26.55438555,"ap":0.2473,"as":8079.66184002,"t":1690409232184,"x":22,"r":1690409232227}]"""
    let realResult = data |> parseData |> map

    let expected = 
        [ ("FTM-USD",
            [ 
                (23, { ev="XQ"; pair="FTM-USD"; lp=0.0; ls=0.0; bp=0.2576; bs=26.55438555; ap=0.2463; askSize=8079.66184002; t=1690409232182L; x=23; r=1690409232227L })
                (22, { ev="XQ"; pair="FTM-USD"; lp=0.0; ls=0.0; bp=0.2492; bs=26.55438555; ap=0.2473; askSize=8079.66184002; t=1690409232184L; x=22; r=1690409232227L })
            ])
        ]

    let areEqual = compareResults realResult expected

    Assert.That(areEqual, Is.True)

[<Test>]
let ``parseData should correctly parse JSON data`` () =
    let jsonData = """[{"ev":"XQ","pair":"BTC-USD","lp":0,"ls":0,"bp":30000.0,"bs":1.0,"ap":30010.0,"as":1.0,"t":1690409232182,"x":1,"r":1690409232227}]"""
    let parsedData = parseData jsonData
    let expectedData = 
        [{ ev="XQ"; pair="BTC-USD"; lp=0.0; ls=0.0; bp=30000.0; bs=1.0; ap=30010.0; askSize=1.0; t=1690409232182L; x=1; r=1690409232227L }]
    Assert.That(parsedData, Is.EqualTo(expectedData))

[<Test>]
let ``map function should handle multiple pairs`` () =
    let data = 
        """[
            {"ev":"XQ","pair":"BTC-USD","lp":0,"ls":0,"bp":30000.0,"bs":1.0,"ap":30010.0,"as":1.0,"t":1690409232182,"x":1,"r":1690409232227},
            {"ev":"XQ","pair":"ETH-USD","lp":0,"ls":0,"bp":2000.0,"bs":2.0,"ap":2010.0,"as":2.0,"t":1690409232184,"x":2,"r":1690409232227}
        ]"""
    let realResult = data |> parseData |> map

    let expected = 
        [ ("BTC-USD",
            [ (1, { ev="XQ"; pair="BTC-USD"; lp=0.0; ls=0.0; bp=30000.0; bs=1.0; ap=30010.0; askSize=1.0; t=1690409232182L; x=1; r=1690409232227L }) ])
          ("ETH-USD",
            [ (2, { ev="XQ"; pair="ETH-USD"; lp=0.0; ls=0.0; bp=2000.0; bs=2.0; ap=2010.0; askSize=2.0; t=1690409232184L; x=2; r=1690409232227L }) ])
        ]

    let areEqual = compareResults realResult expected

    Assert.That(areEqual, Is.True)
