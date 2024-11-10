module ArbitrageGainerTest.AnnualizedReturnCalcTest

open System
open NUnit.Framework
open ArbitrageGainer.AnnualizedReturnCalc


let validParam = {initInvest=6.0; cumPNL=5.0; durationOfYear=calculateDoY DateTime.Now}

[<SetUp>]
let Setup () =
    ()

[<TestCase("10/24/2024", "Duration of Year should be larger than 0")>]
[<Test>]
let ``when time is before 10/24/2024 date, expecting error for DoY`` (currDate, expectedResult) =
    let currParam = {validParam with durationOfYear=calculateDoY(DateTime.Parse(currDate))}
    let realResult = currParam |> validAnnualizedParamCheck |> errorCheck
    Assert.That(realResult, Is.EqualTo(expectedResult))

[<TestCase(-6.0, "initial investment should be larger than 0")>]
[<Test>]
let ``initial investment is less than 0`` (currInitInvest, expectedResult) =
    let currParam = {validParam with initInvest=currInitInvest}
    let realResult = currParam |> validAnnualizedParamCheck |> errorCheck
    Assert.That(realResult, Is.EqualTo(expectedResult))
    
[<TestCase(-6.0, "PNY should be larger than 0")>]
[<Test>]
let ``PNY is less than 0`` (currPNL, expectedResult) =
    let currParam = {validParam with cumPNL=currPNL}
    let realResult = currParam |> validAnnualizedParamCheck |> errorCheck
    Assert.That(realResult, Is.EqualTo(expectedResult))

[<Test>]
let ``test`` (currInitInvest, expectedResult) =
    Assert.That(1, Is.EqualTo(2))