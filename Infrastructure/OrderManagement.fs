namespace ArbitrageGainer.Infrastructure

open System
open System.Net.Http
open System.Net.Http.Json
open ArbitrageGainer.Services.Repository.OrderRepository
open ArbitrageGainer.Services.Repository.TransactionRepository
open System.Text.Json
open Services.PNLCalculation

module OrderManagement =
    let httpClient = new HttpClient()

    let tryExtractOrderId (response: string) =
        try
            use doc = JsonDocument.Parse(response)
            doc.RootElement
            |> fun root -> root.EnumerateObject()
            |> Seq.tryFind (fun p -> p.Name = "order_id")
            |> Option.map (fun p -> p.Value.GetString())
        with _ ->
            None

    let postWithErrorHandling (url:string) (body: obj) =
        async {
            try
                let! response = httpClient.PostAsJsonAsync(url, body) |> Async.AwaitTask
                match response.IsSuccessStatusCode with
                | true ->
                    let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    return Ok content
                | false ->
                    let! errorContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    return Error errorContent       
            with ex ->
                return Error ex.Message
        }

    let submitOrderToBitfinex (order: Order) =
        let url = "https://api.bitfinex.com/v2/auth/w/order/submit"
        let body =
            {| ``type`` = "EXCHANGE LIMIT"
               symbol = order.CurrencyPair
               amount = order.OrderQuantity
               price = order.OrderPrice |}
        async {
            let! res = postWithErrorHandling url body
            match res with
            | Ok content ->
                match tryExtractOrderId content with
                | Some oid -> return Ok oid
                | None -> return Error "No order_id field in response"
            | Error err -> return Error err
        }

    let submitOrderToKraken (order: Order) =
        let url = "https://api.kraken.com/0/private/AddOrder"
        let body =
            {| nonce = 0
               ordertype = "limit"
               ``type`` = order.Type
               volume = order.OrderQuantity
               price = order.OrderPrice
               pair = order.CurrencyPair
               cl_ord_id = order.OrderId |}
        async {
            let! res = postWithErrorHandling url body
            match res with
            | Ok content ->
                match tryExtractOrderId content with
                | Some oid -> return Ok oid
                | None -> return Error "No order_id field in response"
            | Error err -> return Error err
        }

    let submitOrderToBitstamp (order: Order) = 
        let url =
            match order.Type with
            | "buy" -> "https://www.bitstamp.net/api/v2/buy/market/" + order.CurrencyPair
            | "sell" -> "https://www.bitstamp.net/api/v2/sell/market/" + order.CurrencyPair
            | _ -> "https://www.bitstamp.net/api/v2/buy/market/" + order.CurrencyPair
        let body =
            {| amount = order.OrderQuantity
               client_order_id = order.OrderId |}
        async {
            let! res = postWithErrorHandling url body
            match res with
            | Ok content ->
                match tryExtractOrderId content with
                | Some oid -> return Ok oid
                | None -> return Error "No order_id field in response"
            | Error err -> return Error err
        }

    let retrieveOrderStatusFromBitfinex (orderId: string) (currencyPair: string) =
        let url = sprintf "https://api.bitfinex.com/v2/auth/r/orders/%s:%s/trades" currencyPair orderId
        let body =
            {| SYMBOL = currencyPair
               ID = orderId |}
        postWithErrorHandling url body

    let retrieveOrderStatusFromKraken (orderId: string) =
        let url = "https://api.kraken.com/0/private/QueryOrders"
        let body =
            {| nonce = 0
               txid = orderId |}
        postWithErrorHandling url body

    let retrieveOrderStatusFromBitstamp (orderId: string) =
        let url = "https://www.bitstamp.net/api/v2/order_status/"
        let body =
            {| id = orderId
               client_order_id = orderId |}
        postWithErrorHandling url body

    let emitOrder (order: Order) = 
        match order.Exchange with
        | "Bitfinex" -> submitOrderToBitfinex order
        | "Kraken" -> submitOrderToKraken order
        | "Bitstamp" -> submitOrderToBitstamp order
        | _ -> async { return Error "Exchange not supported" }

    let retrieveOrderStatus (order: Order) =
        match order.Exchange with
        | "Bitfinex" -> retrieveOrderStatusFromBitfinex order.OrderId order.CurrencyPair
        | "Kraken" -> retrieveOrderStatusFromKraken order.OrderId
        | "Bitstamp" -> retrieveOrderStatusFromBitstamp order.OrderId
        | _ -> async { return Error "Exchange not supported" }

    let updatePNLForFilledAmount (order: Order) (filledQuantity: decimal) =
        let orderType =
            match order.Type with
            | "buy" -> Buy
            | _ -> Sell

        let tradePNL = calculatePNLForTrade { 
            OrderId = Guid.NewGuid(); 
            OrderType = orderType; 
            Amount = filledQuantity; 
            Price = order.OrderPrice; 
            Timestamp = order.Timestamp 
        }
        updateCumulativePNL tradePNL

    let rec processOrder (order: Order) =
        async {
            let! emitResult = emitOrder order
            match emitResult with
            | Ok newOrderId ->
                let updatedOrder = { order with OrderId = newOrderId }
                createOrder updatedOrder |> ignore
                do! Async.Sleep(5000)
                let! orderStatusRes = retrieveOrderStatus updatedOrder
                match orderStatusRes with
                | Ok content ->
                    let parts = content.Split('-')
                    let status = parts.[0]
                    let filled = decimal (parts.[1])
                    updateOrderStatus (updatedOrder.OrderId, status, filled) |> ignore
                    let desiredQty = updatedOrder.OrderQuantity

                    match status with
                    | "Fulfilled" ->
                        updatePNLForFilledAmount updatedOrder desiredQty
                        return Ok content

                    | "PartiallyFulfilled" ->
                        match filled > 0m with
                        | true ->
                            updatePNLForFilledAmount updatedOrder filled
                            let remaining = desiredQty - filled
                            match remaining > 0m with
                            | true ->
                                let orderId2 = Guid.NewGuid().ToString()
                                let newOrder = { updatedOrder with OrderId = orderId2; OrderQuantity = remaining }
                                createOrder newOrder |> ignore
                                let! processResult1 = processOrder newOrder
                                return processResult1
                            | false ->
                                return Ok "No remainder, treated as full fill"
                        | false ->
                            // Not filled at all
                            printfn "Order not filled. Notifying user..."
                            return Ok content

                    | "NotFilled" ->
                        printfn "Order not filled. Notifying user..."
                        return Ok content

                    | _ ->
                        return Error "Unknown order status"
                | Error error -> return Error error
            | Error error -> return Error error
        }