namespace TradingAlgorithm

module TradingAlgorithm =
    open System.Text.Json
    open System
    open System.Text.Json.Serialization
    open ArbitrageGainer.Infrastructure.OrderManagement
    open ArbitrageGainer.Services.Repository.OrderRepository

    // Define the DataMessage type with all necessary fields
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

    // Trading parameters
    let minimalPriceSpreadValue = 0.05
    let minimalTransactionProfit = 5.0
    let maximalTotalTransactionValue = 2000.0
    let maximalTradingValue = 5000.0

    // Map of exchange IDs to exchange names
    let exchangeNames = Map.ofList [
        (6, "BitStamp")
        (23, "Kraken")
        (2, "BitFinex")
        // Add other exchange mappings as needed
    ]

    let getExchangeName exchangeId =
        Map.tryFind exchangeId exchangeNames |> Option.defaultValue (sprintf "Exchange%d" exchangeId)


    // Function to generate a unique key for an arbitrage opportunity
    let createOpportunityKey pair buyExId sellExId =
        $"{pair}:{buyExId}->{sellExId}"

    let evaluateOpportunity pair (buyExchangeId, buyMsg) (sellExchangeId, sellMsg) =
        match (buyExchangeId <> sellExchangeId, sellMsg.BidPrice - buyMsg.AskPrice) with
        | (true, priceSpread) when priceSpread >= minimalPriceSpreadValue ->
            let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
            let potentialProfit = priceSpread * maxQuantity
            match (potentialProfit >= minimalTransactionProfit, buyMsg.AskPrice * maxQuantity <= maximalTotalTransactionValue) with
            | (true, true) ->
                Some (pair, buyExchangeId, sellExchangeId, buyMsg.AskPrice, sellMsg.BidPrice, maxQuantity, potentialProfit)
            | _ -> None
        | _ -> None

    let ProcessCache(cache: Map<string, DataMessage>, cumulativeTradingValue: float, executedArbitrage: Map<string, DateTime>) =
        cache
        |> Map.toList
        |> List.map (fun (key, msg) ->
            let parts = key.Split('.')
            let pair = parts.[0]
            (pair, msg)
        )
        |> List.groupBy fst
        |> List.fold (fun (updatedValue, executedMap) (pair, msgs) ->
            let dataMessages = msgs |> List.map snd
            let dataByExchange =
                dataMessages
                |> List.groupBy (fun msg -> msg.ExchangeId)
                |> List.map (fun (exchangeId, msgs) ->
                    let latestMsg = msgs |> List.maxBy (fun msg -> msg.Timestamp)
                    (exchangeId, latestMsg)
                )

            let arbitrageOpportunities =
                dataByExchange
                |> List.collect (fun buyData ->
                    dataByExchange
                    |> List.choose (fun sellData ->
                        evaluateOpportunity pair buyData sellData
                    )
                )

            match arbitrageOpportunities |> List.tryHead with
            | Some (pair, buyExId, sellExId, buyPrice, sellPrice, quantity, _) ->
                let opportunityKey = createOpportunityKey pair buyExId sellExId
                let now = DateTime.UtcNow
                match Map.tryFind opportunityKey executedMap with
                | Some lastExecuted ->
                    (updatedValue, executedMap)
                | _ ->
                    let totalTransactionValue = buyPrice * quantity
                    let adjustedQuantity =
                        match totalTransactionValue with
                        | v when v > maximalTotalTransactionValue -> maximalTotalTransactionValue / buyPrice
                        | _ -> quantity

                    let adjustedTotalTransactionValue = buyPrice * adjustedQuantity
                    let finalQuantity =
                        match updatedValue + adjustedTotalTransactionValue with
                        | v when v > maximalTradingValue -> (maximalTradingValue - updatedValue) / buyPrice
                        | _ -> adjustedQuantity

                    match finalQuantity > 0.0 with
                    | true ->
                        let newCumulativeTradingValue = updatedValue + (buyPrice * finalQuantity)
                        let buyExchangeName = getExchangeName buyExId
                        let sellExchangeName = getExchangeName sellExId

                        let baseOrder = {
                            OrderId = ""
                            CurrencyPair = pair
                            Type = ""
                            OrderQuantity = decimal finalQuantity
                            FilledQuantity = 0M
                            OrderPrice = decimal buyPrice
                            Exchange = buyExchangeName 
                            Status = ""
                            TransactionId = ""
                            Timestamp = DateTime.UtcNow
                        }
                        processOrderLegs baseOrder sellExchangeName (decimal sellPrice)
                        
                        printfn "%s, %d (%s) Buy, %f, %f" pair buyExId buyExchangeName buyPrice finalQuantity
                        printfn "%s, %d (%s) Sell, %f, %f" pair sellExId sellExchangeName sellPrice finalQuantity
                        let newExecutedMap = executedMap.Add(opportunityKey, now)
                        (newCumulativeTradingValue, newExecutedMap)
                    | false -> (updatedValue, executedMap)
            | None -> (updatedValue, executedMap)
        ) (cumulativeTradingValue, executedArbitrage)
