module ArbitrageGainerTest.PNLCalculationTest

open NUnit.Framework
open FsUnit
open Services.PNLCalculation
open Domain

[<TestFixture>]
type PNLCalculationTests() =

    [<SetUp>]
    member _.Setup() =
        // Reset PNL values before each test
        setPNLThreshold 0.0m |> ignore
        cumulativePNL <- 0.0m
        tradingActive <- true

    [<Test>]
    member _.``Set PNL Threshold with valid value`` () =
        let result = setPNLThreshold 1000.0m
        result |> should equal (Ok ())

    [<Test>]
    member _.``Set PNL Threshold with negative value`` () =
        let result = setPNLThreshold (-1000.0m)
        match result with
        | Error (InvalidPNLThreshold msg) ->
            msg |> should equal "Threshold must be non-negative"
        | _ -> Assert.Fail("Expected InvalidPNLThreshold error")

    [<Test>]
    member _.``Update cumulative PNL and check threshold not reached`` () =
        setPNLThreshold 1000.0m |> ignore
        updateCumulativePNL 500.0m
        let status = getCurrentPNLStatus()
        status.CurrentPNL |> should equal 500.0m
        status.ThresholdReached |> should be False

    [<Test>]
    member _.``Update cumulative PNL and check threshold reached`` () =
        setPNLThreshold 1000.0m |> ignore
        updateCumulativePNL 1500.0m
        let status = getCurrentPNLStatus()
        status.CurrentPNL |> should equal 1500.0m
        status.ThresholdReached |> should be True

    [<Test>]
    member _.``Get historical PNL with valid dates`` () =
        // Assuming getHistoricalPNL function works correctly
        // Since we can't access the database in tests, we can mock the function
        let startDate = System.DateTime.Now.AddDays(-7.0)
        let endDate = System.DateTime.Now
        let totalPNL = getHistoricalPNL startDate endDate
        totalPNL |> should be (greaterThanOrEqualTo 0.0m)
