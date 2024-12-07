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
        (6, "Bitstamp")
        (23, "Kraken")
        (2, "Bitfinex")
        // Add other exchange mappings as needed
    ]

    let getExchangeName exchangeId =
        Map.tryFind exchangeId exchangeNames |> Option.defaultValue (sprintf "Exchange%d" exchangeId)

    // Function to generate a unique key for an arbitrage opportunity
    let createOpportunityKey pair buyExId sellExId =
        $"{pair}:{buyExId}->{sellExId}"

    let evaluateOpportunity pair (buyExchangeId, buyMsg) (sellExchangeId, sellMsg) =
        let priceSpread = sellMsg.BidPrice - buyMsg.AskPrice
        match buyExchangeId <> sellExchangeId && priceSpread >= minimalPriceSpreadValue with
        | true ->
            let maxQuantity = min buyMsg.AskSize sellMsg.BidSize
            let potentialProfit = priceSpread * maxQuantity
            match potentialProfit >= minimalTransactionProfit && buyMsg.AskPrice * maxQuantity <= maximalTotalTransactionValue with
            | true -> Some (pair, buyExchangeId, sellExchangeId, buyMsg.AskPrice, sellMsg.BidPrice, maxQuantity, potentialProfit)
            | false -> None
        | false -> None

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
        |> List.map (fun (pair, group) -> (pair, group |> List.map snd))

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
                match totalTransactionValue > maximalTotalTransactionValue with
                | true -> maximalTotalTransactionValue / buyPrice
                | false -> quantity

            let adjustedTotalTransactionValue = buyPrice * adjustedQuantity
            let finalQuantity = 
                match cumulativeValue + adjustedTotalTransactionValue > maximalTradingValue with
                | true -> (maximalTradingValue - cumulativeValue) / buyPrice
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
            | false -> (cumulativeValue, executedMap)

    //---------------------------------------------------------------------------
    // 3. Process a single group's messages
    //    (float * Map<string,DateTime>) -> string * DataMessage list -> (float * Map<string,DateTime>)
    //---------------------------------------------------------------------------
    let processPairGroup (cumulativeTradingValue, executedArbitrage) (pair, msgs) =
        match findArbitrageOpportunities pair msgs with
        | [] -> (cumulativeTradingValue, executedArbitrage)
        | arbitrageOpportunities ->
            let bestOpportunity = arbitrageOpportunities |> List.maxBy (fun (_, _, _, _, _, _, potentialProfit) -> potentialProfit)
            executeOpportunity bestOpportunity (cumulativeTradingValue, executedArbitrage)

    //---------------------------------------------------------------------------
    // 4. Process all pair groups
    //    (float * Map<string, DateTime>) -> (string * DataMessage list) list -> (float * Map<string, DateTime>)
    //---------------------------------------------------------------------------
    let processAllPairGroups (cumulativeTradingValue, executedArbitrage) groups =
        groups |> List.fold processPairGroup (cumulativeTradingValue, executedArbitrage)

    //---------------------------------------------------------------------------
    // Refactored ProcessCache
    //---------------------------------------------------------------------------
    let ProcessCache (cache: Map<string, DataMessage>, cumulativeTradingValue: float, executedArbitrage: Map<string, DateTime>) =
        cache
        |> extractPairMessages
        |> groupMessagesByPair
        |> processAllPairGroups (cumulativeTradingValue, executedArbitrage)
