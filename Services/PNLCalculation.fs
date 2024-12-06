namespace Services

open System
open Domain

module PNLCalculation =

    type OrderType = Buy | Sell

    type Trade = {
        OrderId: Guid
        OrderType: OrderType
        Amount: decimal
        Price: decimal
        Timestamp: DateTime
    }

    type PNLStatus = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
        TradingActive: bool
    }

    type PNLState = PNLStatus

    // Initial state with no mutable variables and no if statements
    let initialPNLState = {
        CurrentPNL = 0.0m
        Threshold = None
        ThresholdReached = false
        TradingActive = true
    }

    type PNLCalculationError =
        | InvalidPNLThreshold of string

    type PNLMessage =
        | GetState of AsyncReplyChannel<PNLState>
        | UpdatePNL of decimal
        | SetThreshold of decimal * AsyncReplyChannel<Result<unit, PNLCalculationError>>
        | GetStatus of AsyncReplyChannel<PNLStatus>
        | GetCumulativePNL of AsyncReplyChannel<decimal>
        | GetHistoricalPNL of DateTime * DateTime * AsyncReplyChannel<decimal>

    let calculatePNLForTrade (trade: Trade) : decimal =
        match trade.OrderType with
        | Sell -> trade.Price * trade.Amount
        | Buy -> -(trade.Price * trade.Amount)

    let checkPNLThresholdReached (state: PNLState) : PNLState =
        match state.Threshold with
        | Some threshold ->
            // Instead of if: pattern match on whether currentPNL >= threshold
            match state.CurrentPNL >= threshold with
            | true ->
                { state with ThresholdReached = true; TradingActive = false; Threshold = None }
            | false -> state
        | None -> state

    let rec getHistoricalPNLInternal (startDate: DateTime) (endDate: DateTime) : decimal =
        // Placeholder implementation
        // In a real scenario, fetch historical trades and sum their P&L.
        0.0m

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
                    // Instead of if statements, use pattern matching on threshold value
                    match threshold with
                    | t when t < 0.0m ->
                        reply.Reply(Error (InvalidPNLThreshold "Threshold must be non-negative"))
                        return! loop state
                    | 0.0m ->
                        let newState = { state with Threshold = None }
                        reply.Reply(Ok ())
                        return! loop newState
                    | _ ->
                        let newState = { state with Threshold = Some threshold }
                        reply.Reply(Ok ())
                        return! loop newState

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
