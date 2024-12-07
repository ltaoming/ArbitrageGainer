module ArbitrageGainer.Infrastructure.OrderManagement

open System
open System.Text
open EmailSender
open System.Net.Http
open System.Net.Http.Json
open ArbitrageGainer.Services.Repository.OrderRepository
open ArbitrageGainer.Services.Repository.TransactionRepository
open System.Text.Json
open Microsoft.AspNetCore.Authentication
open Notification
open Services.PNLCalculation

let httpClient = new HttpClient()

let postWithErrorHanding (url:string) (body: string) =
    async {
        try
            let requestBody = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            let! response = httpClient.PostAsync(url, requestBody) |> Async.AwaitTask
            match response.IsSuccessStatusCode with
            | true ->
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                printfn "Sent order info"
                return Ok content
            | _ ->
                let! errorContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error errorContent
        with
        | ex -> return Error ex.Message
    }

let submitOrderToBitfinex (order: Order) =
    async {
        let url = "https://one8656-testing-server.onrender.com/order/place/v2/auth/w/order/submit"
        let symbol = "t" + order.CurrencyPair.Replace("-", "")
        let amount =
            match order.Type with
            | "buy" -> order.OrderQuantity
            | "sell" -> -order.OrderQuantity
        let body = sprintf "type=MARKET&symbol=%s&amount=%f&price=%f" symbol amount order.OrderPrice
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let orderId = json.RootElement.[4].[0].[0].GetInt32().ToString()
            return Ok orderId
        | Error error -> return Error error
    }

let submitOrderToKraken (order: Order) =
    async {
        let url = "https://one8656-testing-server.onrender.com/order/place/0/private/AddOrder"
        let symbol = "XX" + order.CurrencyPair.Replace("-", "")
        let body = sprintf "nonce=1&ordertype=market&type=%s&volume=%f&pair=%s&price=%f" order.Type order.OrderQuantity symbol order.OrderPrice
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let orderId = json.RootElement.GetProperty("result").GetProperty("txid").[0].GetString()
            return Ok orderId
        | Error error -> return Error error
    }

let submitOrderToBitstamp (order: Order) =
    async {
        let symbol = order.CurrencyPair.Replace("-", "").ToLower()
        let url = sprintf "https://one8656-testing-server.onrender.com/order/place/api/v2/%s/market/%s/" order.Type symbol
        let body = sprintf "amount=%f&price=%f" order.OrderQuantity order.OrderPrice
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let orderId = json.RootElement.GetProperty("id").GetString()
            return Ok orderId
        | Error error -> return Error error
    }

let retrieveOrderStatusFromBitfinex (order: Order) =
    async {
        let symbol = "t" + order.CurrencyPair.Replace("-", "")
        let url = sprintf "https://one8656-testing-server.onrender.com/order/status/auth/r/order/%s:%s/trades" symbol order.OrderId
        let body = ""
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let filledQuantity = json.RootElement.[0].[4].GetDecimal() |> abs
            return Ok filledQuantity
        | Error error -> return Error error
    }

let retrieveOrderStatusFromKraken (order: Order) =
    async {
        let url = "https://one8656-testing-server.onrender.com/order/status/0/private/QueryOrders"
        let body = sprintf "nonce=1&txid=%s&trades=true" order.OrderId
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let filledQuantity = json.RootElement.GetProperty("result").GetProperty(order.OrderId).GetProperty("vol_exec").GetString() |> System.Decimal.Parse
            return Ok filledQuantity
        | Error error -> return Error error
    }

let retrieveOrderStatusFromBitstamp (order: Order) =
    async {
        let url = "https://one8656-testing-server.onrender.com/order/status/api/v2/order_status/"
        let body = sprintf "id=%s" order.OrderId
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let amountRemaining = json.RootElement.GetProperty("amount_remaining").GetString() |> System.Decimal.Parse
            let filledQuantity = order.OrderQuantity - amountRemaining
            return Ok filledQuantity
        | Error error -> return Error error
    }

let emitOrder (order: Order) = 
    let pnlStatus = getCurrentPNLStatus() |> Async.RunSynchronously
    match pnlStatus.TradingActive with
    | false ->
        // When TradingActive = false, it means that the P&L threshold has been reached.
        match pnlStatus.ThresholdReached with
        | true -> notifyUserOfPLThresholdReached (pnlStatus.Threshold |> Option.defaultValue 0.0m)
        | false -> ()
        async { return Error "Trading is stopped due to P&L threshold reached." }
    | true ->
        match order.Exchange with
        | "Bitfinex" -> submitOrderToBitfinex order
        | "Kraken" -> submitOrderToKraken order
        | "Bitstamp" -> submitOrderToBitstamp order
        | _ -> async { return Error "Exchange not supported" }

let retrieveOrderStatus (order: Order) =
    match order.Exchange with
    | "Bitfinex" -> retrieveOrderStatusFromBitfinex order
    | "Kraken" -> retrieveOrderStatusFromKraken order
    | "Bitstamp" -> retrieveOrderStatusFromBitstamp order
    | _ -> async { return Error "Exchange not supported" }

let storeNewOrder (order: Order) (orderId: String) =
    let newOrder = { order with OrderId = orderId }
    createOrder newOrder
    |> Result.bind (fun _ -> Ok newOrder)

let retrieveNewOrderAsync (order: Order) =
    async {
        do! Async.Sleep(5000)
        let! orderStatus = retrieveOrderStatus order
        printfn "Retrieved order status"
        return
            orderStatus
            |> Result.bind (fun filledAmount ->
                let status =
                    match filledAmount with
                    | amt when amt >= order.OrderQuantity -> "FullyFilled"
                    | 0.0m -> "NotFilled"
                    | _  -> "PartiallyFulfilled"
                let newOrder = { order with FilledQuantity = filledAmount; Status = status }
                Ok newOrder)
    }
    |> Async.RunSynchronously

let updateFilledQuantityInOrder (newOrder: Order) =
    updateOrderStatus newOrder
    |> Result.bind (fun _ -> Ok newOrder)

let notifyUserOfOrderStatusUpdate (orderId: string) (orderStatus: string) =
    let emailBody = sprintf "OrderStatus updated: %s" orderStatus
    let emailSubject = "OrderStatus Changed"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore

let notifyUserOfPLThresholdReached (threshold: decimal) =
    let emailBody = sprintf "P&L threshold reached: %M" threshold
    let emailSubject = "P&L Threshold has been reached"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore

let rec processOrder (order: Order) =
    async {
        let! result = emitOrder order
        return
            result
            |> Result.bind (storeNewOrder order)
            |> Result.bind retrieveNewOrderAsync
            |> Result.bind updateFilledQuantityInOrder
            |> Result.bind (fun (order: Order) ->
                match order.Status with
                | "PartiallyFulfilled" ->
                    notifyUserOfOrderStatusUpdate order.OrderId "PartiallyFulfilled"
                    let newOrder = { order with OrderQuantity = order.OrderQuantity - order.FilledQuantity }
                    processOrder newOrder |> Async.RunSynchronously
                | "NotFilled" ->
                    notifyUserOfOrderStatusUpdate order.OrderId "NotFilled"
                    Ok "Order not filled"
                | "FullyFilled" ->
                    notifyUserOfOrderStatusUpdate order.OrderId "FullyFilled"
                    Ok "Order fully filled"
                | _ -> Ok "Order status unknown"
            )
    }

let processOrderLegs (order: Order) (sellExchangeName: string) (sellPrice: decimal) (buyExchangeName: string) (buyPrice: decimal) =
    let transactionId = Guid.NewGuid().ToString()

    let order1 = { order with OrderId = ""; Type = "buy";  OrderPrice = buyPrice; Exchange = buyExchangeName; TransactionId = transactionId }
    let order2 = { order with OrderId = ""; Type = "sell"; OrderPrice = sellPrice; Exchange = sellExchangeName; TransactionId = transactionId }
    
    printfn "Submitting Order1: %A" order1
    printfn "Submitting Order2: %A" order2

    let results = [processOrder order1; processOrder order2] |> Async.Parallel |> Async.RunSynchronously
    let processResult1, processResult2 = results.[0], results.[1]
    
    match processResult1, processResult2 with
    | Ok status1, Ok status2 ->
        printfn "Both orders processed successfully - Order1 Status: '%s'; Order2 Status: '%s'" status1 status2
        match status1, status2 with
        | "FullyFilled", "FullyFilled" ->
            updateTransactionStatus (transactionId, "Completed") |> ignore
            storeTransactionHistory transactionId |> ignore
            printfn "Transaction %s completed successfully." transactionId
        | ("FullyFilled", _)
        | (_, "FullyFilled") ->
            let filledOrderId = if status1 = "FullyFilled" then "Order1" else "Order2"
            Notification.notifyUserOfOrderStatusUpdate filledOrderId "OneSideFilled"
            updateTransactionStatus (transactionId, "OneSideFilled") |> ignore
            storeTransactionHistory transactionId |> ignore
            printfn "One side filled scenario handled."
        | _ ->
            printfn "Unhandled order status combination."
    | Ok status1, Error error2 ->
        printfn "Order1 processed successfully - Status: '%s'; Order2 Error: '%s'" status1 error2
    | Error error1, Ok status2 ->
        printfn "Order1 Error: '%s'; Order2 processed successfully - Status: '%s'" error1 status2
    | Error error1, Error error2 ->
        printfn "Both orders have Errors - Order1: '%s'; Order2: '%s'" error1 error2
