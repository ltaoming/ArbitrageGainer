module ArbitrageGainer.Infrastructure.OrderManagement

open System
open System.Text
open System.Net.Http
open System.Net.Http.Json
open ArbitrageGainer.Services.Repository.OrderRepository
open ArbitrageGainer.Services.Repository.TransactionRepository
open System.Text.Json

let httpClient = new HttpClient()

let postWithErrorHanding (url:string) (body: string) =
    async {
        try
            let requestBody = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            let! response = httpClient.PostAsync(url, requestBody) |> Async.AwaitTask
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
    async {
        let url = "https://one8656-testing-server.onrender.com/order/place/v2/auth/w/order/submit"
        let symbol = "t" + order.CurrencyPair.Replace("-", "")
        let body = sprintf "type=MARKET&symbol=%s&amount=%f&price=%f" symbol order.OrderQuantity order.OrderPrice
        let! response = postWithErrorHanding url body
        match response with
        | Ok content ->
            let json = JsonDocument.Parse(content)
            let orderId = json.RootElement.[0].[4].GetString()
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
            let orderId = json.RootElement.GetProperty("txid").GetString()
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
            let filledQuantity = json.RootElement.[0].GetDecimal()
            // if sell, it means the amount sold
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
            let filledQuantity = json.RootElement.GetProperty("result").GetProperty("vol_exec").GetDecimal()
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
            let amountRemaining = json.RootElement.GetProperty("amount_remaining").GetDecimal()
            let filledQuantity = order.OrderQuantity - amountRemaining
            return Ok filledQuantity
        | Error error -> return Error error
    }

let emitOrder (order:Order) = 
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

let storeNewOrder (order: Order) (orderId: String) =
    let newOrder = { order with OrderId = orderId }
    createOrder newOrder
    |> Result.bind (fun _ -> Ok newOrder)

let retrieveNewOrderAsync (order: Order) =
    async {
        do! Async.Sleep(5000)
        let! orderStatus = retrieveOrderStatus order
        return orderStatus
        |> Result.bind (fun filledAmount ->
            let status =
                let orderQuantity = order.OrderQuantity
                match filledAmount with
                | _ when filledAmount = order.OrderQuantity -> "FullyFilled"
                | 0.0m -> "NotFilled"
                | _ -> "PartiallyFulfilled"
            let newOrder = { order with FilledQuantity = filledAmount; Status = status }
            Ok newOrder)
    }
    |> Async.RunSynchronously
    
let updateFilledQuantityInOrder (newOrder: Order) =
    updateOrderStatus newOrder
    |> Result.bind (fun _ -> Ok newOrder)

let rec processOrder (order: Order) =
    async {
        let! result = emitOrder order
        return result
            |> Result.bind (storeNewOrder order)
            |> Result.bind retrieveNewOrderAsync
            |> Result.bind updateFilledQuantityInOrder
            |> Result.bind (fun (order:Order) ->
                match order.Status with
                | "PartiallyFulfilled" ->
                    // recursive call to process partially filled order
                    let newOrder = { order with OrderQuantity = order.OrderQuantity - order.FilledQuantity }
                    processOrder newOrder |> Async.RunSynchronously
                | "NotFilled" ->
                    // TODO: notify user that a this is not filled
                    Ok "Order not filled"
                | _ -> Ok "Order fully filled"
            )
    }

// let processOrderLegs (order: Order) =
//     let ransactionId = Guid.NewGuid().ToString()
//     // order 1: buy, order 2: sell
//     let order1 = { order with OrderId = ""; Status = "buy"; TransactionId = ransactionId}
//     let order2 = { order with OrderId = ""; Status = "sell"; TransactionId = ransactionId}
//     // submit order and retrieve status
//     let results = [processOrder order1; processOrder order2] |> Async.Parallel |> Async.RunSynchronously
//     let processResult1, processResult2 = results.[0], results.[1]
//     
//     match processResult1, processResult2 with
//     | Ok content1, Ok content2 ->
//         printfn "Both orders processed successfully - order1: '%s'; order2: '%s'" content1 content2
//     | Error error1, Error error2 -> printfn "Both orders have Errors - order1: '%s'; order2: '%s'" error1 error2
//     | Error error, _ -> printfn "order 1 has Error - order1: '%s''" error
//     | _, Error error -> printfn "order 2 has Error - order2: '%s'" error
    
let processOrderLegs (order: Order) (sellExchangeName: string) (sellPrice: decimal) =
    let ransactionId = Guid.NewGuid().ToString()
    // order 1: buy, order 2: sell
    let order1 = { order with OrderId = ""; Status = "buy"; TransactionId = ransactionId}
    let order2 = { order with OrderId = ""; Status = "sell"; OrderPrice = sellPrice; Exchange = sellExchangeName; TransactionId = ransactionId}
    // submit order and retrieve status
    let results = [processOrder order1; processOrder order2] |> Async.Parallel |> Async.RunSynchronously
    let processResult1, processResult2 = results.[0], results.[1]
    
    match processResult1, processResult2 with
    | Ok content1, Ok content2 ->
        printfn "Both orders processed successfully - order1: '%s'; order2: '%s'" content1 content2
    | Error error1, Error error2 -> printfn "Both orders have Errors - order1: '%s'; order2: '%s'" error1 error2
    | Error error, _ -> printfn "order 1 has Error - order1: '%s''" error
    | _, Error error -> printfn "order 2 has Error - order2: '%s'" error

// let processOrderLegs (order: Order) =
//
//     let transaction = { TransactionId = Guid.NewGuid().ToString()
//                         Status = "Submitted"
//                         ListOfOrderIds = []
//                         Timestamp = DateTime.UtcNow }
//     let createResult = createTransaction transaction
//     match createResult with
//     | Ok _ ->
//         // create order
//         let order1 = { order with OrderId = ""; Status = "buy"}
//         let order2 = { order with OrderId = ""; Status = "sell"}
//         let orderResult1 = createOrder order1
//         let orderResult2 = createOrder order2
//         match orderResult1, orderResult2 with
//         | Ok _, Ok _ ->
//             // submit order and retrieve status
//             let processResult1 = processOrder order1 |> Async.RunSynchronously
//             let processResult2 = processOrder order2 |> Async.RunSynchronously
//             match processResult1, processResult2 with
//             | Ok content1, Ok content2 ->
//                 // both status retrieved, try get if either one is partially filled
//                 let orderIdListResult = getOrdersFromTransaction transaction.TransactionId
//                 let stringResult =
//                     match orderIdListResult with
//                     | Ok orderIdList ->
//                         orderIdList
//                         |> List.map (fun orderId ->
//                             let resultFromDB = getOrder orderId
//                             match resultFromDB with
//                             | Ok order -> order
//                             | Error error -> failwith error)
//                         |> List.filter (fun order -> order.Status = "PartiallyFulfilled")
//                         |> List.map (fun order -> processPartiallyOrder order)
//                         |> List.reduce (fun acc x ->
//                             match acc, x with
//                             | Ok _, Ok _ -> Ok "Order processed successfully"
//                             | Error error1, Error error2 -> Error (error1 + " " + error2)
//                             | Error error, _ -> Error error
//                             | _, Error error -> Error error)
//                         // Ok "Order processed successfully"
//                     | Error error -> Error error  
//                 match stringResult with
//                 | Ok _ -> Ok "Order processed successfully"
//                 | Error error -> Error error
//             | Error error1, Error error2 -> Error (error1 + " " + error2)
//             | Error error, _ -> Error error
//             | _, Error error -> Error error
//             
//         | Error error1, Error error2 -> Error (error1 + " " + error2)
//         | Error error, _ -> Error error
//         | _, Error error -> Error error
//     | Error error -> Error error