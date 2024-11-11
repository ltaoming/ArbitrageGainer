module ArbitrageGainerTest.HistoryArbitrageOpportunityTest

open System
open NUnit.Framework
open ArbitrageGainer.HistoryArbitrageOpportunity

[<SetUp>]
let Setup () =
    ()


[<Test>]
let ``can identify opportunity across exchanges`` () =
    let data = 
        """[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2576,"bs":26.55438555,"ap":0.2463,"as":8079.66184002,"t":1690409232182,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2492,"bs":26.55438555,"ap":0.2473,"as":8079.66184002,"t":1690409232184,"x":22,"r":1690409232227}]"""
    let realResult = data |> parseData |> calculateHistoryArbitrageOpportunity
    let expected = ["FTM-USD, 1 opportunities"]
    Assert.That(realResult,Is.EqualTo(expected))

[<Test>]
let ``can merge pairs with 5ms`` () =
    let data = 
        """[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":1.00,"bs":26.55438555,"ap":1.05,"as":8079.66184002,"t":1690409232184,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2473,"bs":26.55438555,"ap":0.2492,"as":8079.66184002,"t":1690409232186,"x":22,"r":1690409232227}]"""
    let realResult = data |> parseData|> calculateHistoryArbitrageOpportunity 
    let expected = []
    Assert.That(realResult,Is.EqualTo(expected))
    
[<Test>]
let ``can ignore price difference of less than 0.01$`` () =
    let data = 
        """[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2469,"bs":26.55438555,"ap":0.2472,"as":8079.66184002,"t":1690409232184,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2473,"bs":26.55438555,"ap":0.2475,"as":8079.66184002,"t":1690409232186,"x":22,"r":1690409232227}]"""
    let realResult = data |> parseData|> calculateHistoryArbitrageOpportunity 
    let expected = []
    Assert.That(realResult,Is.EqualTo(expected))
    
[<Test>]
let ``can identify multiple pairs for opportunities`` () =
    let data = 
        """[{"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2576,"bs":26.55438555,"ap":0.2463,"as":8079.66184002,"t":1690409232181,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"FTM-USD","lp":0,"ls":0,"bp":0.2492,"bs":26.55438555,"ap":0.2473,"as":8079.66184002,"t":1690409232184,"x":22,"r":1690409232227},
            {"ev":"XQ","pair":"BTC-USD","lp":0,"ls":0,"bp":0.2576,"bs":26.55438555,"ap":0.2463,"as":8079.66184002,"t":1690409232181,"x":23,"r":1690409232227},
            {"ev":"XQ","pair":"BTC-USD","lp":0,"ls":0,"bp":0.2492,"bs":26.55438555,"ap":0.2473,"as":8079.66184002,"t":1690409232184,"x":22,"r":1690409232227}]"""
    let realResult = data |> parseData|> calculateHistoryArbitrageOpportunity
    let expected = ["FTM-USD, 1 opportunities"; "BTC-USD, 1 opportunities"]
    Assert.That(realResult,Is.EqualTo(expected))