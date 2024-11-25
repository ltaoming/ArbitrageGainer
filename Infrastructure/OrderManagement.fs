module ArbitrageGainer.Infrastructure.OrderManagement

open System
open System.Net.Http
open System.Net.Http.Json
open ArbitrageGainer.Services.Repository.OrderRepository
open ArbitrageGainer.Services.Repository.TransactionRepository
open System.Text.Json

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
                // both status retrieved, try get if either one is partially filled
                let orderIdListResult = getOrdersFromTransaction transaction.TransactionId
                let stringResult =
                    match orderIdListResult with
                    | Ok orderIdList ->
                        orderIdList
                        |> List.map (fun orderId ->
                                        let resultFromDB = getOrder orderId
                                        match resultFromDB with
                                        | Ok order -> order
                                        | Error error -> failwith error)
                        |> List.filter (fun order -> order.Status = "PartiallyFulfilled")
                        |> List.map (fun order -> processPartiallyOrder order)
                        |> List.reduce (fun acc x ->
                                        match acc, x with
                                        | Ok _, Ok _ -> Ok "Order processed successfully"
                                        | Error error1, Error error2 -> Error (error1 + " " + error2)
                                        | Error error, _ -> Error error
                                        | _, Error error -> Error error)
                        // Ok "Order processed successfully"
                    | Error error -> Error error  
                match stringResult with
                | Ok _ -> Ok "Order processed successfully"
                | Error error -> Error error
            | Error error1, Error error2 -> Error (error1 + " " + error2)
            | Error error, _ -> Error error
            | _, Error error -> Error error
            
        | Error error1, Error error2 -> Error (error1 + " " + error2)
        | Error error, _ -> Error error
        | _, Error error -> Error error
    | Error error -> Error error