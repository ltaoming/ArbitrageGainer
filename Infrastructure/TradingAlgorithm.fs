namespace TradingAlgorithm

module TradingAlgorithm =
    open System
    open System.Text.Json.Serialization
    open ArbitrageGainer.Infrastructure.OrderManagement
    open ArbitrageGainer.Services.Repository.OrderRepository

    type TradingParams = {
        MinimalPriceSpreadValue: float
        MinimalTransactionProfit: float
        MaximalTotalTransactionValue: float
        MaximalTradingValue: float
    }

    type DataMessage = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("pair")>]
        Pair: string
        [<JsonPropertyName("lp")>]
        LastPrice: float
        [<JsonPropertyName("ls")>]
        LastSize: float
        [<JsonPropertyName("bp")>]
        BidPrice: float
        [<JsonPropertyName("bs")>]
        BidSize: float
        [<JsonPropertyName("ap")>]
        AskPrice: float
        [<JsonPropertyName("as")>]
        AskSize: float
        [<JsonPropertyName("t")>]
        Timestamp: int64
        [<JsonPropertyName("x")>]
        ExchangeId: int
    }

    let exchangeNames = Map.ofList [
        (6, "Bitstamp")
        (23, "Kraken")
        (2, "Bitfinex")
        // Add more exchanges if needed
    ]

    let getExchangeName exchangeId =
        Map.tryFind exchangeId exchangeNames |> Option.defaultValue (sprintf "Exchange%d" exchangeId)

    let createOpportunityKey pair buyExId sellExId =
        $"{pair}:{buyExId}->{sellExId}"

    let evaluateOpportunity tp pair (buyExchangeId, buyMsg) (sellExchangeId, sellMsg) =
        let priceSpread = sellMsg.BidPrice - buyMsg.AskPrice
        let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
        let potentialProfit = priceSpread * maxQuantity

        match (buyExchangeId <> sellExchangeId,
               priceSpread >= tp.MinimalPriceSpreadValue,
               potentialProfit >= tp.MinimalTransactionProfit,
               (buyMsg.AskPrice * maxQuantity) <= tp.MaximalTotalTransactionValue) with
        | (true, true, true, true) ->
            Some (pair, buyExchangeId, sellExchangeId, buyMsg.AskPrice, sellMsg.BidPrice, maxQuantity, potentialProfit)
        | _ ->
            None

    let extractPairMessages (cache: Map<string, DataMessage>) =
        cache
        |> Map.toList
        |> List.map (fun (key, msg) ->
            let parts = key.Split('.')
            let pair = parts.[0]
            (pair, msg)
        )

    let groupMessagesByPair pairMessages =
        pairMessages
        |> List.groupBy fst
        |> List.map (fun (pair, group) ->
            let messagesOnly = group |> List.map snd
            (pair, messagesOnly)
        )

    let findArbitrageOpportunities tp pair (msgs: DataMessage list) =
        let dataByExchange =
            msgs
            |> List.groupBy (fun msg -> msg.ExchangeId)
            |> List.map (fun (exId, ms) -> (exId, ms |> List.maxBy (fun m -> m.Timestamp)))

        dataByExchange
        |> List.collect (fun buyData ->
            dataByExchange
            |> List.choose (fun sellData ->
                evaluateOpportunity tp pair buyData sellData
            )
        )

    let executeOpportunity tp (pair, buyExId, sellExId, buyPrice, sellPrice, quantity, _) (cumulativeValue, executedMap) =
        let opportunityKey = createOpportunityKey pair buyExId sellExId
        let now = DateTime.UtcNow

        match Map.tryFind opportunityKey executedMap with
        | Some _ ->
            (cumulativeValue, executedMap)
        | None ->
            let totalTransactionValue = buyPrice * quantity
            let adjustedQuantity =
                match totalTransactionValue > tp.MaximalTotalTransactionValue with
                | true -> tp.MaximalTotalTransactionValue / buyPrice
                | false -> quantity

            let adjustedTotalTransactionValue = buyPrice * adjustedQuantity

            let finalQuantity =
                match (cumulativeValue + adjustedTotalTransactionValue) > tp.MaximalTradingValue with
                | true -> (tp.MaximalTradingValue - cumulativeValue) / buyPrice
                | false -> adjustedQuantity

            match finalQuantity > 0.0 with
            | true ->
                let newCumulativeTradingValue = cumulativeValue + (buyPrice * finalQuantity)
                let buyExchangeName = getExchangeName buyExId
                let sellExchangeName = getExchangeName sellExId

                let baseOrder = {
                    OrderId = ""
                    CurrencyPair = pair
                    Type = ""
                    OrderQuantity = decimal finalQuantity
                    FilledQuantity = 0M
                    OrderPrice = decimal 0
                    Exchange = buyExchangeName
                    Status = ""
                    TransactionId = ""
                    Timestamp = DateTime.UtcNow
                }

                processOrderLegs baseOrder sellExchangeName (decimal sellPrice) buyExchangeName (decimal buyPrice)

                printfn "%s, %d (%s) Buy, %f, %f" pair buyExId buyExchangeName buyPrice finalQuantity
                printfn "%s, %d (%s) Sell, %f, %f" pair sellExId sellExchangeName sellPrice finalQuantity

                let newExecutedMap = executedMap.Add(opportunityKey, now)
                (newCumulativeTradingValue, newExecutedMap)
            | false ->
                (cumulativeValue, executedMap)

    let processPairGroup tp (cumulativeTradingValue, executedArbitrage) (pair, msgs) =
        let arbitrageOpportunities = findArbitrageOpportunities tp pair msgs
        match arbitrageOpportunities with
        | [] -> (cumulativeTradingValue, executedArbitrage)
        | _ ->
            let bestOpportunity =
                arbitrageOpportunities
                |> List.maxBy (fun (_, _, _, _, _, _, potentialProfit) -> potentialProfit)
            executeOpportunity tp bestOpportunity (cumulativeTradingValue, executedArbitrage)

    let processAllPairGroups tp (cumulativeTradingValue, executedArbitrage) groups =
        groups
        |> List.fold (processPairGroup tp) (cumulativeTradingValue, executedArbitrage)

    let ProcessCache (cache: Map<string, DataMessage>, cumulativeTradingValue: float, executedArbitrage: Map<string, DateTime>, tpOpt: TradingParams option) =
        match tpOpt with
        | None ->
            (cumulativeTradingValue, executedArbitrage)
        | Some tp ->
            cache
            |> extractPairMessages
            |> groupMessagesByPair
            |> processAllPairGroups tp (cumulativeTradingValue, executedArbitrage)
