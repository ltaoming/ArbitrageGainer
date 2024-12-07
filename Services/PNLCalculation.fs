namespace Services

open System
open Domain
open ArbitrageGainer.Services.Repository.OrderRepository

module PNLCalculation =

    type OrderType =
        | Buy
        | Sell

    type Trade = {
        OrderId: string
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

    type PNLCalculationError =
        | InvalidPNLThreshold of string

    type PNLMessage =
        | GetState of AsyncReplyChannel<PNLStatus>
        | UpdatePNL of decimal
        | SetThreshold of decimal * AsyncReplyChannel<Result<unit, PNLCalculationError>>
        | GetStatus of AsyncReplyChannel<PNLStatus>
        | GetCumulativePNL of AsyncReplyChannel<decimal>
        | GetHistoricalPNL of DateTime * DateTime * AsyncReplyChannel<decimal>

    let initialPNLState = {
        CurrentPNL = 0.0m
        Threshold = None
        ThresholdReached = false
        TradingActive = true
    }

    let checkPNLThresholdReached (state: PNLStatus) : PNLStatus =
        match state.Threshold with
        | Some threshold when state.CurrentPNL >= threshold ->
            { state with ThresholdReached = true; TradingActive = false; Threshold = None }
        | _ -> state

    // Retrieves trades from the database for a given period and converts them into Trade records.
    let getArbitrageTrades (startDate: DateTime) (endDate: DateTime) : Trade list =
        match getOrdersInPeriod startDate endDate with
        | Ok orders ->
            // Convert orders to trades
            orders
            |> List.map (fun o ->
                let t =
                    match o.Type.ToLowerInvariant() with
                    | "buy" -> Buy
                    | "sell" -> Sell
                    | _ -> failwith "Unknown order type"
                {
                    OrderId = o.OrderId
                    OrderType = t
                    Amount = o.FilledQuantity
                    Price = o.OrderPrice
                    Timestamp = o.Timestamp
                }
            )
        | Error _ ->
            // If there's an error or no orders found, return empty list
            []

    // Calculate P&L for a single trade.
    // For a buy order: P&L contribution is negative (cost).
    // For a sell order: P&L contribution is positive (revenue).
    let calculatePNLForTrade (trade: Trade) : decimal =
        match trade.OrderType with
        | Buy -> -(trade.Price * trade.Amount)
        | Sell -> trade.Price * trade.Amount

    let pnlAgent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (state: PNLStatus) = async {
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
                    | t when t < 0.0m ->
                        reply.Reply(Error (InvalidPNLThreshold "Threshold must be non-negative"))
                        return! loop state
                    | 0.0m ->
                        let newState = { state with Threshold = None }
                        reply.Reply(Ok ())
                        return! loop newState
                    | t ->
                        let newState = { state with Threshold = Some t }
                        reply.Reply(Ok ())
                        return! loop newState

                | GetStatus reply ->
                    reply.Reply(state)
                    return! loop state

                | GetCumulativePNL reply ->
                    reply.Reply(state.CurrentPNL)
                    return! loop state

                | GetHistoricalPNL (startDate, endDate, reply) ->
                    let trades = getArbitrageTrades startDate endDate
                    let totalPNL = trades |> List.map calculatePNLForTrade |> List.sum
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