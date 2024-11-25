module ArbitrageGainerTest.AnnualizedReturnCalcTest

open System
open NUnit.Framework
open ArbitrageGainer.AnnualizedReturnCalc

[<SetUp>]
let Setup () =
    ()

[<Test>]
let ``when time is before 10/24/2024 date, expecting error for DoY`` () =
    let startDate = DateTime.Parse("10/24/2024")
    let currDate = DateTime.Parse("10/05/2024")
    let durationOfYear = calculateDoY startDate currDate
    let currParam = { initInvest = 4.0; cumPNL = 5.0; durationOfYear = durationOfYear }
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo("Duration of Year should be larger than 0"))
    | Ok _ -> Assert.Fail()

[<Test>]
let ``when initial investment is less than 0, expecting an error`` () =
    let startDate = DateTime.Parse("11/24/2024")
    let currDate = DateTime.Now
    let durationOfYear = calculateDoY startDate currDate
    let currParam = { initInvest = -6.0; cumPNL = 5.0; durationOfYear = durationOfYear }
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo("Initial investment should be larger than 0"))
    | Ok _ -> Assert.Fail()

[<Test>]
let ``when PNL is less than 0, expecting an error`` () =
    let startDate = DateTime.Parse("11/24/2024")
    let currDate = DateTime.Now
    let durationOfYear = calculateDoY startDate currDate
    let currParam = { initInvest = 4.0; cumPNL = -6.0; durationOfYear = durationOfYear }
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Error msg -> Assert.That(msg, Is.EqualTo("PNL should be non-negative"))
    | Ok _ -> Assert.Fail()

[<Test>]
let ``annualizedReturn returns normally by following the flow`` () =
    let startDate = DateTime.Parse("11/24/2024")
    let currDate = DateTime.Now
    let durationOfYear = calculateDoY startDate currDate
    let currParam = { initInvest = 4.0; cumPNL = 5.0; durationOfYear = durationOfYear }
    let realResult = currParam |> validAnnualizedParamCheck
    match realResult with
    | Ok _ ->
        let result = calculateAnnualizedReturn currParam
        // Adjust the expected result based on the current date
        // Since DateTime.Now changes, the expected result should be calculated accordingly
        let expectedResult = ((currParam.cumPNL / currParam.initInvest) ** (1.0 / currParam.durationOfYear)) - 1.0
        Assert.That(abs(result - expectedResult), Is.LessThanOrEqualTo(0.01))
    | Error _ -> Assert.Fail()
