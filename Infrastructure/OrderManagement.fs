module ArbitrageGainer.Infrastructure.OrderManagement

open System
open EmailSender
open System.Net.Http
open System.Net.Http.Json
open ArbitrageGainer.Services.Repository.OrderRepository
open ArbitrageGainer.Services.Repository.TransactionRepository
open System.Text.Json
open Notification
open Services.PNLCalculation
let httpClient = new HttpClient()

let postWithErrorHanding (url:string) (body: obj) =
    async {
        try
            let! response = httpClient.PostAsJsonAsync(url, body) |> Async.AwaitTask
            match response.IsSuccessStatusCode with
            | true ->
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Ok content
            | _ ->
                let! errorContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error errorContent       
        with
        | ex -> return Error ex.Message
    }

let submitOrderToBitfinex (order: Order) =
    let url = "https://api.bitfinex.com/v2/auth/w/order/submit"
    let body =
        {| ``type`` = "EXCHANGE LIMIT"
           symbol = order.CurrencyPair
           amount = order.OrderQuantity
           price = order.OrderPrice |}
    postWithErrorHanding url body

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
    postWithErrorHanding url body

let submitOrderToBitstamp (order: Order) = 
    let url =
        match order.Type with
        | "buy" -> "https://www.bitstamp.net/api/v2/buy/market/" + order.CurrencyPair
        | "sell" -> "https://www.bitstamp.net/api/v2/sell/market/" + order.CurrencyPair
    let body =
        {| amount = order.OrderQuantity
           client_order_id = order.OrderId |}
    postWithErrorHanding url body
    
let retrieveOrderStatusFromBitfinex (order: Order) =
    let url = sprintf "https://api.bitfinex.com/v2/auth/r/orders/%s:%s/trades" order.CurrencyPair order.OrderId
    let body =
        {| SYMBOL = order.CurrencyPair
           ID = order.OrderId |}
    postWithErrorHanding url body

let retrieveOrderStatusFromKraken (order: Order) =
    let url = "https://api.kraken.com/0/private/QueryOrders"
    let body =
        {| nonce = 0
           txid = order.OrderId |}
    postWithErrorHanding url body

let retrieveOrderStatusFromBitstamp (order: Order) =
    let url = "https://www.bitstamp.net/api/v2/order_status/"
    let body =
        {| id = order.OrderId
           client_order_id = order.OrderId |}
    postWithErrorHanding url body

let emitOrder (order:Order) = 
    let pnlStatus = getCurrentPNLStatus() |> Async.RunSynchronously
    match pnlStatus.TradingActive with
    | false ->
        // When TradingActive = false, it means that the threshold has been reached.
        // Here you can check if ThresholdReached is true, and if so, call the email notification
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


let retrieveOrderStatus (order:Order) =
    match order.Exchange with
    | "Bitfinex" -> retrieveOrderStatusFromBitfinex order
    | "Kraken" -> retrieveOrderStatusFromKraken order
    | "Bitstamp" -> retrieveOrderStatusFromBitstamp order
    | _ -> async { return Error "Exchange not supported" }

let processOrder (order: Order) =
    async {
        let! result = emitOrder order
        match result with
        | Ok _ ->
            do! Async.Sleep(5000)
            let! orderStatus = retrieveOrderStatus order
            match orderStatus with
                | Ok content ->
                    let parts = content.Split('-')
                    updateOrderStatus (order.OrderId, parts.[0], Decimal.Parse(parts.[1])) |> ignore
                    return Ok content
                | Error error -> return Error error
        | Error error -> return Error error
    }
    


let processPartiallyOrder (order: Order) =
    let orderId = Guid.NewGuid().ToString()
    let newOrder = { order with OrderId = orderId; OrderQuantity = order.OrderQuantity - order.FilledQuantity }
    let orderResult1 = createOrder newOrder
    match orderResult1 with
    | Ok _ ->
        let processResult1 = processOrder newOrder |> Async.RunSynchronously
        match processResult1 with
        | Ok _ -> Ok "Order processed successfully"
        | Error error -> Error error
    | Error error -> Error error

// let identifyPartiallyFulfilledAndProcess (transactionId: string) =
//     
//     let orderIdListResult = getOrdersFromTransaction transactionId
//     match orderIdListResult with
//     | Ok orderIdList ->
//         orderIdList
//         |> List.map (fun orderId ->
//             let resultFromDB = getOrder orderId
//             match resultFromDB with
//             | Ok order -> order
//             | Error error -> failwith error)
//         |> List.filter (fun order -> order.Status = "PartiallyFulfilled")
//         |> List.iter (fun order -> processPartiallyOrder order |> Async.RunSynchronously)
//         Ok "Order processed successfully"
//     | Error error -> Error error

let notifyUserOfOrderStatusUpdate (orderId: string) (orderStatus: string) =
    let emailBody = sprintf "OrderStatus updated:：%s" orderStatus
    let emailSubject = "OrderStatus Changed"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore
let notifyUserOfPLThresholdReached (threshold: decimal) =
    let emailBody = sprintf "P&L threshold reached: %M" threshold
    let emailSubject = "P&L Threshold has been reached"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore
let processOrderLegs (order: Order) =
    // create transaction
    let orderId1 = Guid.NewGuid().ToString()
    let orderId2 = Guid.NewGuid().ToString()

    let transaction = { TransactionId = Guid.NewGuid().ToString()
                        Status = "Submitted"
                        ListOfOrderIds = [orderId1; orderId2]
                        Timestamp = DateTime.UtcNow }
    let createResult = createTransaction transaction
    match createResult with
    | Ok _ ->
        // create order
        let order1 = { order with OrderId = orderId1; Status = "buy"}
        let order2 = { order with OrderId = orderId2; Status = "sell"}
        let orderResult1 = createOrder order1
        let orderResult2 = createOrder order2
        match orderResult1, orderResult2 with
        | Ok _, Ok _ ->
            // submit order and retrieve status
            let processResult1 = processOrder order1 |> Async.RunSynchronously
            let processResult2 = processOrder order2 |> Async.RunSynchronously
            match processResult1, processResult2 with
            | Ok content1, Ok content2 ->
                let orderIdListResult = getOrdersFromTransaction transaction.TransactionId
                let stringResult =
                    match orderIdListResult with
                    | Ok orderIdList ->
                        let orders =
                            orderIdList
                            |> List.map (fun orderId ->
                                let resultFromDB = getOrder orderId
                                match resultFromDB with
                                | Ok order -> order
                                | Error error -> failwith error)

                        let buyOrder = orders |> List.find (fun o -> o.OrderId = orderId1)
                        let sellOrder = orders |> List.find (fun o -> o.OrderId = orderId2)

                        // 新增状态判断逻辑
                        match buyOrder.Status, sellOrder.Status with
                        | "FullyFulfilled", "FullyFulfilled" ->
                            // 两侧完全成交，原逻辑不变，根据业务需要持久化交易历史
                            updateTransactionStatus (transaction.TransactionId, "Completed") |> ignore
                            storeTransactionHistory transaction.TransactionId |> ignore
                            Ok "Both sides fully fulfilled."
                        
                        | "FullyFulfilled", otherStatus when otherStatus <> "FullyFulfilled" && otherStatus <> "PartiallyFulfilled" ->
                            // Buy单已全成交，Sell单未成交或失败
                            // 发邮件通知用户只一侧成交
                            notifyUserOfOrderStatusUpdate buyOrder.OrderId "OneSideFilled"
                            // 持久化交易状态和历史
                            updateTransactionStatus (transaction.TransactionId, "OneSideFilled") |> ignore
                            storeTransactionHistory transaction.TransactionId |> ignore
                            Ok "One side filled scenario handled."

                        | otherStatus, "FullyFulfilled" when otherStatus <> "FullyFulfilled" && otherStatus <> "PartiallyFulfilled" ->
                            // Sell单已全成交，Buy单未成交或失败
                            notifyUserOfOrderStatusUpdate sellOrder.OrderId "OneSideFilled"
                            updateTransactionStatus (transaction.TransactionId, "OneSideFilled") |> ignore
                            storeTransactionHistory transaction.TransactionId |> ignore
                            Ok "One side filled scenario handled."

                        | _ ->
                            // 保留原有逻辑处理 PartiallyFulfilled 等场景
                            orders
                            |> List.filter (fun order -> order.Status = "PartiallyFulfilled")
                            |> List.map (fun order -> processPartiallyOrder order)
                            |> List.reduce (fun acc x ->
                                match acc, x with
                                | Ok _, Ok _ -> Ok "Order processed successfully"
                                | Error error1, Error error2 -> Error (error1 + " " + error2)
                                | Error error, _ -> Error error
                                | _, Error error -> Error error)

                    | Error error -> Error error

                match stringResult with
                | Ok _ -> 
                    notifyUserOfOrderStatusUpdate orderId1 "PartiallyFulfilled"
                    Ok "Order processed successfully"
                | Error error -> Error error

            | Error error1, Error error2 -> Error (error1 + " " + error2)
            | Error error, _ -> Error error
            | _, Error error -> Error error
            
        | Error error1, Error error2 -> Error (error1 + " " + error2)
        | Error error, _ -> Error error
        | _, Error error -> Error error
    | Error error -> Error error