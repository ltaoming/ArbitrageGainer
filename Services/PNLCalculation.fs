namespace Services

open System
open Domain
open ArbitrageGainer.Services.Repository.OrderRepository
open Notification // For emailing the user
open System.Collections.Generic

module PNLCalculation =

    type OrderType =
        | Buy
        | Sell

    // A trade represents a completed portion of an order
    type Trade = {
        CurrencyPair: string
        OrderId: string
        OrderType: OrderType
        Amount: decimal
        Price: decimal
        Timestamp: DateTime
    }

    // PNLStatus represents the current state of P&L
    // Inventory: Map<CurrencyPair, (Quantity, WeightedAverageCost)>
    type PNLStatus = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
        TradingActive: bool
        Inventory: Map<string, (decimal * decimal)>
    }

    type PNLCalculationError =
        | InvalidPNLThreshold of string

    type PNLMessage =
        | GetState of AsyncReplyChannel<PNLStatus>
        | SetThreshold of decimal * AsyncReplyChannel<Result<unit, PNLCalculationError>>
        | GetStatus of AsyncReplyChannel<PNLStatus>
        | GetCumulativePNL of AsyncReplyChannel<decimal>
        | GetHistoricalPNL of DateTime * DateTime * AsyncReplyChannel<decimal>
        | ProcessCompletedOrder of Order * AsyncReplyChannel<unit>

    let initialPNLState = {
        CurrentPNL = 0.0m
        Threshold = None
        ThresholdReached = false
        TradingActive = true
        Inventory = Map.empty
    }

    let checkPNLThresholdReached (state: PNLStatus) : PNLStatus =
        match state.Threshold with
        | Some threshold when state.CurrentPNL >= threshold ->
            // According to the spec: send email notification, stop trading, reset threshold
            notifyUserOfPLThresholdReached threshold
            { state with ThresholdReached = true; TradingActive = false; Threshold = None }
        | _ -> state

    // Update inventory after a buy: Weighted Average Cost recalculation
    let updateInventoryBuy (inventory: Map<string,(decimal*decimal)>) (pair: string) (amount: decimal) (price: decimal) =
        let (oldQty, oldCost) =
            match Map.tryFind pair inventory with
            | Some (q, c) -> (q, c)
            | None -> (0.0m, 0.0m)

        let newQty = oldQty + amount
        let totalOldCost = oldQty * oldCost
        let totalNewCost = totalOldCost + (amount * price)
        let newCost =
            match newQty with
            | q when q > 0.0m -> totalNewCost / newQty
            | _ -> 0.0m

        inventory |> Map.add pair (newQty, newCost)

    // Update inventory and realize P&L after a sell:
    // P&L = (sale price - avg cost) * amount sold
    let updateInventorySell (inventory: Map<string,(decimal*decimal)>) (currentPNL: decimal) (pair: string) (amount: decimal) (price: decimal) =
        match Map.tryFind pair inventory with
        | Some (q, c) when q >= amount ->
            let profit = (price - c) * amount
            let newQty = q - amount
            let newInv =
                match newQty with
                | x when x > 0.0m -> inventory |> Map.add pair (newQty, c)
                | _ -> inventory |> Map.remove pair
            (newInv, currentPNL + profit)
        | Some (_, c) ->
            // Not enough inventory; project specs do not mention short selling scenario
            // If reached here, assume no negative inventory allowed, no profit since invalid scenario:
            (inventory, currentPNL)
        | None ->
            // Selling without any inventory - not defined in spec. If occurs, treat entire revenue as profit:
            let profit = price * amount
            (inventory, currentPNL + profit)

    // Convert an Order into a Trade
    let orderToTrade (o: Order) =
        let t =
            match o.Type.ToLowerInvariant() with
            | "buy" -> Buy
            | "sell" -> Sell
            | _ -> failwith "Unknown order type"
        {
            CurrencyPair = o.CurrencyPair
            OrderId = o.OrderId
            OrderType = t
            Amount = o.FilledQuantity
            Price = o.OrderPrice
            Timestamp = o.Timestamp
        }

    let getArbitrageTrades (startDate: DateTime) (endDate: DateTime) : Trade list =
        // Only fully filled orders are considered for historical P&L
        // If partial fills count, they must also be stored as separate trades or considered similarly.
        // The code currently only retrieves FullyFilled orders, which is consistent with final profit scenario.
        match getOrdersInPeriod startDate endDate with
        | Ok orders ->
            orders |> List.map orderToTrade
        | Error _ ->
            []

    // Replay trades to compute historical P&L
    let rec computeHistoricalPNL (trades: Trade list) (state: PNLStatus) =
        match trades with
        | [] -> state.CurrentPNL
        | trade::rest ->
            let updatedState =
                match trade.OrderType with
                | Buy ->
                    { state with Inventory = updateInventoryBuy state.Inventory trade.CurrencyPair trade.Amount trade.Price }
                | Sell ->
                    let (newInv, newPNL) = updateInventorySell state.Inventory state.CurrentPNL trade.CurrencyPair trade.Amount trade.Price
                    { state with Inventory=newInv; CurrentPNL=newPNL }
            computeHistoricalPNL rest updatedState

    let pnlAgent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (state: PNLStatus) = async {
                let! msg = inbox.Receive()
                match msg with
                | GetState reply ->
                    reply.Reply(state)
                    return! loop state

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
                    // Rebuild P&L from scratch for given period
                    let trades = getArbitrageTrades startDate endDate
                    let replayState = { state with CurrentPNL=0.0m; Inventory=Map.empty; Threshold=None; ThresholdReached=false; TradingActive=true }
                    let totalPNL = computeHistoricalPNL trades replayState
                    reply.Reply(totalPNL)
                    return! loop state

                | ProcessCompletedOrder (order, reply) ->
                    // Process each completed order (fully or partially)
                    // Update inventory and P&L accordingly
                    let trade = orderToTrade order
                    let newState =
                        match trade.OrderType with
                        | Buy ->
                            let newInv = updateInventoryBuy state.Inventory trade.CurrencyPair trade.Amount trade.Price
                            { state with Inventory=newInv } |> checkPNLThresholdReached
                        | Sell ->
                            let (newInv, newPNL) = updateInventorySell state.Inventory state.CurrentPNL trade.CurrencyPair trade.Amount trade.Price
                            { state with Inventory=newInv; CurrentPNL=newPNL } |> checkPNLThresholdReached

                    reply.Reply(())
                    return! loop newState
            }
            loop initialPNLState
        )

    // Public API functions for external calls
    let setPNLThreshold (threshold: decimal) : Async<Result<unit, PNLCalculationError>> =
        pnlAgent.PostAndAsyncReply(fun reply -> SetThreshold (threshold, reply))

    let getCurrentPNLStatus () : Async<PNLStatus> =
        pnlAgent.PostAndAsyncReply(GetState)

    let getCumulativePNL () : Async<decimal> =
        pnlAgent.PostAndAsyncReply(GetCumulativePNL)

    let getHistoricalPNL (startDate: DateTime) (endDate: DateTime) : Async<decimal> =
        pnlAgent.PostAndAsyncReply(fun reply -> GetHistoricalPNL (startDate, endDate, reply))

    let processCompletedOrder (order: Order) : Async<unit> =
        pnlAgent.PostAndAsyncReply(fun reply -> ProcessCompletedOrder(order, reply))