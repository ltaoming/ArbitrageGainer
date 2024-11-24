namespace Services

open System
open Domain
open ArbitrageGainer.Database

module PNLCalculation =

    type PNLStatus = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
    }

    let mutable currentPNLThreshold: decimal option = None
    let mutable cumulativePNL: decimal = 0.0m
    let mutable tradingActive: bool = true

    let setPNLThreshold (threshold: decimal) =
        if threshold > 0.0m then
            currentPNLThreshold <- Some threshold
            Ok ()
        elif threshold = 0.0m then
            currentPNLThreshold <- None
            Ok ()
        else
            Error (InvalidPNLThreshold "Threshold must be non-negative")

    let checkPNLThresholdReached () =
        match currentPNLThreshold with
        | Some threshold when cumulativePNL >= threshold ->
            // Threshold reached
            tradingActive <- false
            // Reset the threshold
            currentPNLThreshold <- None
            // For now, just return the status
            true
        | _ ->
            false

    let updateCumulativePNL (additionalPNL: decimal) =
        cumulativePNL <- cumulativePNL + additionalPNL
        // Check if threshold reached
        let _ = checkPNLThresholdReached ()
        ()

    let getCurrentPNLStatus () =
        {
            CurrentPNL = cumulativePNL
            Threshold = currentPNLThreshold
            ThresholdReached = not tradingActive
        }

    let getArbitrageTrades (startDate: DateTime) (endDate: DateTime) =
        // Placeholder for actual implementation
        []

    let calculatePNLForTrade trade =
        // Placeholder for actual implementation
        0.0m

    let getHistoricalPNL (startDate: DateTime) (endDate: DateTime) =
        let trades = getArbitrageTrades startDate endDate
        let pnlPerTrade =
            trades
            |> Seq.map calculatePNLForTrade
        let totalPNL = pnlPerTrade |> Seq.sum
        totalPNL

    let getCumulativePNL () =
        cumulativePNL
