module ArbitrageGainerTest.AnnualizedReturnCalcTest

open System
open NUnit.Framework
open ArbitrageGainer.AnnualizedReturnCalc


let validParam = {initInvest=4.0; cumPNL=5.0; durationOfYear=calculateDoY(DateTime.Parse("11/24/2024"))}

[<SetUp>]
let Setup () =
    ()


[<TestCase("10/24/2024", "Duration of Year should be larger than 0")>]
[<TestCase("10/05/2024", "Duration of Year should be larger than 0")>]
[<Test>]
let ``when time is before 10/24/2024 date, expecting error for DoY`` (currDate, expectedResult) =
    let currParam = {validParam with durationOfYear=calculateDoY(DateTime.Parse(currDate))}
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo(expectedResult))
    | Ok _ -> Assert.Fail()

[<TestCase(-6.0, "initial investment should be larger than 0")>]
[<TestCase(0.0, "initial investment should be larger than 0")>]
[<Test>]
let ``when initial investment is less than 0, expecting an error`` (currInitInvest, expectedResult) =
    let currParam = {validParam with initInvest=currInitInvest}
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo(expectedResult))
    | Ok _ -> Assert.Fail()
    
[<TestCase(-6.0, "PNY should be larger than 0")>]
[<TestCase(0.0, "PNY should be larger than 0")>]
[<Test>]
let ``when PNY is less than 0, expecting an error`` (currPNL, expectedResult) =
    let currParam = {validParam with cumPNL=currPNL}
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo(expectedResult))
    | Ok _ -> Assert.Fail()
    
[<TestCase(4.0, 5.0, "11/24/2024", 12.83685)>]
[<Test>]
let ``annualizedReturn return normally by following the flow`` (currInitInvest, currPNL, currDate, expectedResult) =
    let currParam = {initInvest=currInitInvest; cumPNL=currPNL; durationOfYear=calculateDoY(DateTime.Parse(currDate))}
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Ok msg ->
        let result = calculateAnnualizedReturn currParam
        Assert.That(abs(result - expectedResult), Is.LessThanOrEqualTo(0.01))
    | Error _ -> Assert.Fail()
