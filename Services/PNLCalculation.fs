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

    type PNLCalculationError =
        | InvalidPNLThreshold of string

    type PNLMessage =
        | GetState of AsyncReplyChannel<PNLState>
        | UpdatePNL of decimal
        | SetThreshold of decimal * AsyncReplyChannel<Result<unit, PNLCalculationError>>
        | GetStatus of AsyncReplyChannel<PNLStatus>
        | GetCumulativePNL of AsyncReplyChannel<decimal>
        | GetHistoricalPNL of DateTime * DateTime * AsyncReplyChannel<decimal>

    let rec getHistoricalPNLInternal (startDate: DateTime) (endDate: DateTime) : decimal =
        // Placeholder for actual implementation of retrieving trades
        getArbitrageTrades startDate endDate
        |> List.map calculatePNLForTrade
        |> List.sum

    and getArbitrageTrades (startDate: DateTime) (endDate: DateTime) : Trade list =
        // Implement retrieval of trades from database between startDate and endDate
        []

    and calculatePNLForTrade (trade: Trade) : decimal =
        // Implement P&L calculation logic for a single trade
        // For demonstration purposes, we'll assume P&L is Price * Amount for sell orders
        // and negative Price * Amount for buy orders
        match trade.OrderType with
        | Sell -> trade.Price * trade.Amount
        | Buy -> -(trade.Price * trade.Amount)

    let pnlAgent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (state: PNLState) = async {
                let! msg = inbox.Receive()
                match msg with
                | GetState reply ->
                    reply.Reply(state)
                    return! loop state
                | UpdatePNL additionalPNL ->
                    let updatedState = { state with CurrentPNL = state.CurrentPNL + additionalPNL }
                    let newState = checkPNLThresholdReached updatedState
                    return! loop newState
                | SetThreshold (threshold, reply) ->
                    match threshold with
                    | t when t >= 0.0m ->
                        let newState =
                            if t = 0.0m then
                                { state with Threshold = None }
                            else
                                { state with Threshold = Some t }
                        reply.Reply(Ok ())
                        return! loop newState
                    | _ ->
                        reply.Reply(Error (InvalidPNLThreshold "Threshold must be non-negative"))
                        return! loop state
                | GetStatus reply ->
                    reply.Reply(state)
                    return! loop state
                | GetCumulativePNL reply ->
                    reply.Reply(state.CurrentPNL)
                    return! loop state
                | GetHistoricalPNL (startDate, endDate, reply) ->
                    let totalPNL = getHistoricalPNLInternal startDate endDate
                    reply.Reply(totalPNL)
                    return! loop state
            }
            loop initialPNLState
        )

    // Functions to interact with the agent
    let setPNLThreshold (threshold: decimal) : Async<Result<unit, PNLCalculationError>> =
        pnlAgent.PostAndAsyncReply(fun reply -> SetThreshold (threshold, reply))

    let updateCumulativePNL (additionalPNL: decimal) : unit =
        pnlAgent.Post(UpdatePNL additionalPNL)

    let getCurrentPNLStatus () : Async<PNLStatus> =
        pnlAgent.PostAndAsyncReply(GetStatus)

    let getCumulativePNL () : Async<decimal> =
        pnlAgent.PostAndAsyncReply(GetCumulativePNL)

    let getHistoricalPNL (startDate: DateTime) (endDate: DateTime) : Async<decimal> =
        pnlAgent.PostAndAsyncReply(fun reply -> GetHistoricalPNL (startDate, endDate, reply))
