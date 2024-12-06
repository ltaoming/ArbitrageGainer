namespace TradingAlgorithm

open System
open Infrastructure.TradingParametersAgent // To get trading parameters

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

    let getExchangeName exchangeId =
        let exchangeNames = Map.ofList [
            (6, "BitStamp")
            (23, "Kraken")
            (2, "BitFinex")
        ]
        Map.tryFind exchangeId exchangeNames |> Option.defaultValue (sprintf "Exchange%d" exchangeId)

    let evaluateOpportunity (pair: string) (buyExchangeId, buyMsg: DataMessage) (sellExchangeId, sellMsg: DataMessage) =
        let p = getParameters()
        match buyExchangeId = sellExchangeId with
        | true -> None
        | false ->
            let priceSpread = sellMsg.BidPrice - buyMsg.AskPrice
            match priceSpread >= p.MinimalPriceSpread with
            | true ->
                let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
                let potentialProfit = priceSpread * float maxQuantity
                let totalCost = buyMsg.AskPrice * float maxQuantity
                match (potentialProfit >= p.MinimalTransactionProfit, totalCost <= p.MaximalTransactionValue) with
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
