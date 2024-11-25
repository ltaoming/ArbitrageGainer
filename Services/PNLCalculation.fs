namespace Services

open System
open Domain
open ArbitrageGainer.Database

module PNLCalculation =

    // Define the OrderType and Trade types
    type OrderType = Buy | Sell

    type Trade = {
        OrderId: Guid
        OrderType: OrderType
        Amount: decimal
        Price: decimal
        Timestamp: DateTime
    }

    // PNLStatus represents the current state of P&L
    type PNLStatus = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
        TradingActive: bool
    }

    // PNLState is an alias for PNLStatus for clarity
    type PNLState = PNLStatus

    // Initial state with default values
    let initialPNLState = {
        CurrentPNL = 0.0m
        Threshold = None
        ThresholdReached = false
        TradingActive = true
    }

    // Set PNL Threshold without using if-else and mutable variables
    let setPNLThreshold (threshold: decimal) (state: PNLState) : Result<PNLState, string> =
        match threshold with
        | t when t > 0.0m ->
            Ok { state with Threshold = Some t }
        | 0.0m ->
            Ok { state with Threshold = None }
        | _ ->
            Error "Threshold must be non-negative"

    // Check if PNL Threshold is reached
    let checkPNLThresholdReached (state: PNLState) : PNLState =
        match state.Threshold with
        | Some threshold when state.CurrentPNL >= threshold ->
            // Threshold reached, update state
            { state with
                ThresholdReached = true
                TradingActive = false
                Threshold = None }
        | _ ->
            state

    // Update Cumulative PNL
    let updateCumulativePNL (additionalPNL: decimal) (state: PNLState) : PNLState =
        let updatedState = { state with CurrentPNL = state.CurrentPNL + additionalPNL }
        checkPNLThresholdReached updatedState

    // Get Current PNL Status
    let getCurrentPNLStatus (state: PNLState) : PNLStatus =
        state

    // Placeholder for actual implementation of retrieving trades
    let getArbitrageTrades (startDate: DateTime) (endDate: DateTime) : Trade list =
        // Implement retrieval of trades from database between startDate and endDate
        []

    // Calculate P&L for a single trade
    let calculatePNLForTrade (trade: Trade) : decimal =
        // Implement P&L calculation logic for a single trade
        // For demonstration purposes, we'll assume P&L is Price * Amount for sell orders
        // and negative Price * Amount for buy orders
        match trade.OrderType with
        | Sell -> trade.Price * trade.Amount
        | Buy -> -(trade.Price * trade.Amount)

    // Get Historical PNL
    let getHistoricalPNL (startDate: DateTime) (endDate: DateTime) : decimal =
        getArbitrageTrades startDate endDate
        |> List.map calculatePNLForTrade
        |> List.sum

    // Get Cumulative PNL
    let getCumulativePNL (state: PNLState) : decimal =
        state.CurrentPNL