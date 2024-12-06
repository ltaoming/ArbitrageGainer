namespace Infrastructure

open System
open TradingAlgorithm

module RealTimeTradingLogic =
    let processMarketData
        (cache: Map<string, DataMessage>)
        (cumulativeTradingValue: float)
        (executedArbitrage: Map<string, DateTime>)
        : Map<string, DataMessage> * float * Map<string, DateTime> =

        let groupedByPair : (string * DataMessage list) list =
            cache
            |> Map.toList
            |> List.map (fun (key: string, msg: DataMessage) ->
                let parts: string[] = key.Split('.')
                let pair: string = parts.[0]
                (pair, msg))
            |> List.groupBy fst
            |> List.map (fun (p: string, lst: (string * DataMessage) list) ->
                let onlyMessages: DataMessage list = lst |> List.map snd
                (p, onlyMessages)
            )

        let (finalCache, finalValue, finalExecutedArbitrage) =
            groupedByPair
            |> List.fold (fun (updatedCache, updatedValue, executedMap) (pair: string, dataMessages: DataMessage list) ->
                let dataByExchange: (int * DataMessage) list =
                    dataMessages
                    |> List.groupBy (fun (m: DataMessage) -> m.ExchangeId)
                    |> List.map (fun (exId: int, msgs: DataMessage list) ->
                        let latestMsg = msgs |> List.maxBy (fun mm -> mm.Timestamp)
                        (exId, latestMsg)
                    )

                let arbitrageOpportunities =
                    dataByExchange
                    |> List.collect (fun buyData ->
                        dataByExchange
                        |> List.choose (fun sellData -> TradingAlgorithm.evaluateOpportunity pair buyData sellData)
                    )

                match arbitrageOpportunities |> List.tryHead with
                | Some (pair, buyExId, sellExId, buyMsg, sellMsg, quantity, _) ->
                    let opportunityKey = $"{pair}:{buyExId}->{sellExId}"
                    match Map.tryFind opportunityKey executedMap with
                    | Some _ ->
                        (updatedCache, updatedValue, executedMap)
                    | None ->
                        let totalTransactionValue = buyMsg.AskPrice * quantity
                        let adjustedQuantity =
                            match totalTransactionValue > TradingAlgorithm.maximalTotalTransactionValue with
                            | true ->
                                TradingAlgorithm.maximalTotalTransactionValue / buyMsg.AskPrice
                            | false -> quantity

                        let adjustedTotalTransactionValue = buyMsg.AskPrice * adjustedQuantity
                        let exceedsMaxTradingValue = (updatedValue + adjustedTotalTransactionValue) > TradingAlgorithm.maximalTradingValue
                        let finalQuantity =
                            match exceedsMaxTradingValue with
                            | true ->
                                let leftover = TradingAlgorithm.maximalTradingValue - updatedValue
                                match leftover > 0.0 with
                                | true -> leftover / buyMsg.AskPrice
                                | false -> 0.0
                            | false -> adjustedQuantity

                        match finalQuantity > 0.0 with
                        | true ->
                            let buyExchangeName = TradingAlgorithm.getExchangeName buyExId
                            let sellExchangeName = TradingAlgorithm.getExchangeName sellExId
                            printfn "%s, %d (%s) Buy, %f, %f" pair buyExId buyExchangeName buyMsg.AskPrice finalQuantity
                            printfn "%s, %d (%s) Sell, %f, %f" pair sellExId sellExchangeName sellMsg.BidPrice finalQuantity

                            let newCumulativeTradingValue = updatedValue + (buyMsg.AskPrice * finalQuantity)
                            let newExecutedMap = executedMap.Add(opportunityKey, DateTime.UtcNow)
                            let newCache = TradingAlgorithm.updateLiquidity updatedCache pair buyExId sellExId finalQuantity
                            (newCache, newCumulativeTradingValue, newExecutedMap)
                        | false ->
                            (updatedCache, updatedValue, executedMap)
                | None ->
                    (updatedCache, updatedValue, executedMap)
            ) (cache, cumulativeTradingValue, executedArbitrage)

        (finalCache, finalValue, finalExecutedArbitrage)