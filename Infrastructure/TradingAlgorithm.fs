namespace TradingAlgorithm

type DataMessage = {
    Ev: string
    Pair: string
    LastPrice: float
    LastSize: float
    BidPrice: float
    BidSize: float
    AskPrice: float
    AskSize: float
    Timestamp: int64
    ExchangeId: int
}

module TradingAlgorithm =
    open System

    let minimalPriceSpreadValue = 0.05
    let minimalTransactionProfit = 5.0
    let maximalTotalTransactionValue = 2000.0
    let maximalTradingValue = 5000.0

    let exchangeNames = Map.ofList [
        (6, "BitStamp")
        (23, "Kraken")
        (2, "BitFinex")
    ]

    let getExchangeName exchangeId =
        Map.tryFind exchangeId exchangeNames |> Option.defaultValue (sprintf "Exchange%d" exchangeId)

    let evaluateOpportunity (pair: string) (buyExchangeId, buyMsg: DataMessage) (sellExchangeId, sellMsg: DataMessage) =
        match buyExchangeId = sellExchangeId with
        | true ->
            None
        | false ->
            let priceSpread = sellMsg.BidPrice - buyMsg.AskPrice
            match priceSpread >= minimalPriceSpreadValue with
            | true ->
                let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
                let potentialProfit = priceSpread * float maxQuantity
                let totalCost = buyMsg.AskPrice * float maxQuantity
                match (potentialProfit >= minimalTransactionProfit, totalCost <= maximalTotalTransactionValue) with
                | (true, true) -> Some (pair, buyExchangeId, sellExchangeId, buyMsg, sellMsg, float maxQuantity, potentialProfit)
                | _ -> None
            | false -> None

    let updateLiquidity (cache: Map<string, DataMessage>) pair buyExId sellExId finalQuantity =
        let buyKey = $"{pair}.{buyExId}"
        let sellKey = $"{pair}.{sellExId}"
        let buyMsg = cache.[buyKey]
        let sellMsg = cache.[sellKey]
        let newBuyMsg = { buyMsg with AskSize = buyMsg.AskSize - finalQuantity }
        let newSellMsg = { sellMsg with BidSize = sellMsg.BidSize - finalQuantity }
        cache.Add(buyKey, newBuyMsg).Add(sellKey, newSellMsg)
