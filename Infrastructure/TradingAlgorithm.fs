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
        let priceSpread = sellMsg.BidPrice - buyMsg.AskPrice
        let conditionsMet =
            (buyExchangeId <> sellExchangeId)
            && (priceSpread >= minimalPriceSpreadValue)

        let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
        let potentialProfit = priceSpread * maxQuantity
        let profitCondition = potentialProfit >= minimalTransactionProfit
        let maxValueCondition = (buyMsg.AskPrice * maxQuantity) <= maximalTotalTransactionValue

        if conditionsMet && profitCondition && maxValueCondition then
            Some (pair, buyExchangeId, sellExchangeId, buyMsg.AskPrice, sellMsg.BidPrice, maxQuantity, potentialProfit)
        else
            None

    //---------------------------------------------------------------------------
    // 1. Extract pair messages
    //    Map<string, DataMessage> -> (string * DataMessage) list
    //---------------------------------------------------------------------------
    let extractPairMessages (cache: Map<string, DataMessage>) =
        cache
        |> Map.toList
        |> List.map (fun (key, msg) ->
            let parts = key.Split('.')
            let pair = parts.[0]
            (pair, msg)
        )

    //---------------------------------------------------------------------------
    // 2. Group messages by pair
    //    (string * DataMessage) list -> (string * DataMessage list) list
    //---------------------------------------------------------------------------
    let groupMessagesByPair pairMessages =
        pairMessages
        |> List.groupBy fst
        // Now transform each group's values from (string * DataMessage) list to DataMessage list
        |> List.map (fun (pair, group) ->
            let messagesOnly = group |> List.map snd
            (pair, messagesOnly)
        )

    //---------------------------------------------------------------------------
    // Helper: Given a list of DataMessages for a pair, return all arbitrage opportunities
    //---------------------------------------------------------------------------
    let findArbitrageOpportunities pair (msgs: DataMessage list) =
        let dataByExchange =
            msgs
            |> List.groupBy (fun msg -> msg.ExchangeId)
            |> List.map (fun (exId, ms) -> (exId, ms |> List.maxBy (fun m -> m.Timestamp)))

        dataByExchange
        |> List.collect (fun buyData ->
            dataByExchange
            |> List.choose (fun sellData ->
                evaluateOpportunity pair buyData sellData
            )
        )

    //---------------------------------------------------------------------------
    // Helper: Given an opportunity and current state, attempt execution if not recently executed
    //---------------------------------------------------------------------------
    let executeOpportunity (pair, buyExId, sellExId, buyPrice, sellPrice, quantity, _) (cumulativeValue, executedMap) =
        let opportunityKey = createOpportunityKey pair buyExId sellExId
        let now = DateTime.UtcNow
        match Map.tryFind opportunityKey executedMap with
        | Some _ -> (cumulativeValue, executedMap)
        | None ->
            let totalTransactionValue = buyPrice * quantity
            let adjustedQuantity =
                if totalTransactionValue > maximalTotalTransactionValue then
                    maximalTotalTransactionValue / buyPrice
                else
                    quantity

            let adjustedTotalTransactionValue = buyPrice * adjustedQuantity
            let finalQuantity =
                if cumulativeValue + adjustedTotalTransactionValue > maximalTradingValue then
                    (maximalTradingValue - cumulativeValue) / buyPrice
                else
                    adjustedQuantity

            if finalQuantity > 0.0 then
                let newCumulativeTradingValue = cumulativeValue + (buyPrice * finalQuantity)
                let buyExchangeName = getExchangeName buyExId
                let sellExchangeName = getExchangeName sellExId
                
                let baseOrder = {
                    OrderId = ""
                    CurrencyPair = pair
                    Type = "MARKET"
                    OrderQuantity = decimal finalQuantity
                    FilledQuantity = 0M
                    OrderPrice = decimal buyPrice
                    Exchange = buyExchangeName  // This will be for the buy leg
                    Status = ""
                    TransactionId = ""
                    Timestamp = DateTime.UtcNow
                }
                
                // Now call processOrderLegs to handle the buy and sell.
                // You may need to modify processOrderLegs or this call to also pass in sell exchange/price data 
                // if that logic isn't already handled.
                processOrderLegs baseOrder sellExchangeName (decimal sellPrice)

                printfn "%s, %d (%s) Buy, %f, %f" pair buyExId buyExchangeName buyPrice finalQuantity
                printfn "%s, %d (%s) Sell, %f, %f" pair sellExId sellExchangeName sellPrice finalQuantity

                let newExecutedMap = executedMap.Add(opportunityKey, now)
                (newCumulativeTradingValue, newExecutedMap)
            else
                (cumulativeValue, executedMap)

    //---------------------------------------------------------------------------
    // 3. Process a single group's messages
    //    (float * Map<string,DateTime>) -> string * DataMessage list -> (float * Map<string,DateTime>)
    //---------------------------------------------------------------------------
    let processPairGroup (cumulativeTradingValue, executedArbitrage) (pair, msgs) =
        let arbitrageOpportunities = findArbitrageOpportunities pair msgs
        let bestOpportunity = arbitrageOpportunities |> List.tryHead
        match bestOpportunity with
        | Some opp -> executeOpportunity opp (cumulativeTradingValue, executedArbitrage)
        | None -> (cumulativeTradingValue, executedArbitrage)

    //---------------------------------------------------------------------------
    // 4. Process all pair groups
    //    (float * Map<string, DateTime>) -> (string * DataMessage list) list -> (float * Map<string, DateTime>)
    //---------------------------------------------------------------------------
    let processAllPairGroups (cumulativeTradingValue, executedArbitrage) groups =
        groups
        |> List.fold processPairGroup (cumulativeTradingValue, executedArbitrage)

    //---------------------------------------------------------------------------
    // Refactored ProcessCache
    //---------------------------------------------------------------------------
    let ProcessCache (cache: Map<string, DataMessage>, cumulativeTradingValue: float, executedArbitrage: Map<string, DateTime>) =
        cache
        |> extractPairMessages
        |> groupMessagesByPair
        |> processAllPairGroups (cumulativeTradingValue, executedArbitrage)
