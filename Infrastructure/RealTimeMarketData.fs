namespace RealTimeMarketData

open System
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Text
open TradingAlgorithm

module PolygonWebSocket =

    type Message = { action: string; params: string }

    type StatusMessage = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("status")>]
        Status: string
        [<JsonPropertyName("message")>]
        Message: string
    }

    // Use the DataMessage type from TradingAlgorithm
    type DataMessage = TradingAlgorithm.DataMessage

    // Discriminated union to represent processing results
    type ProcessResult =
        | AuthSuccess
        | AuthFailed of string
        | NoAuthMessage

    // Define messages that the cache agent can handle
    type CacheMessage =
        | Update of key: string * value: DataMessage
        | Get of key: string * reply: AsyncReplyChannel<Option<DataMessage>>
        | GetAll of reply: AsyncReplyChannel<Map<string, DataMessage>>
        | UpdateTradingParams of TradingAlgorithm.TradingParams

    let cacheAgent = MailboxProcessor.Start(fun inbox ->
        let rec loop (cache: Map<string, DataMessage>,
                      cumulativeTradingValue: float,
                      executedArbitrage: Map<string, DateTime>,
                      tpOpt: TradingAlgorithm.TradingParams option) =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Update (key, value) ->
                    let updatedCache =
                        match cache.TryFind key with
                        | Some existingValue when existingValue = value ->
                            cache
                        | _ ->
                            cache.Add(key, value)

                    let newCumulativeTradingValue, newExecutedArbitrage =
                        TradingAlgorithm.TradingAlgorithm.ProcessCache(updatedCache, cumulativeTradingValue, executedArbitrage, tpOpt)

                    return! loop (updatedCache, newCumulativeTradingValue, newExecutedArbitrage, tpOpt)

                | Get (key, reply) ->
                    let value = cache.TryFind key
                    reply.Reply value
                    return! loop (cache, cumulativeTradingValue, executedArbitrage, tpOpt)

                | GetAll reply ->
                    reply.Reply cache
                    return! loop (cache, cumulativeTradingValue, executedArbitrage, tpOpt)

                | UpdateTradingParams newTp ->
                    return! loop (cache, cumulativeTradingValue, executedArbitrage, Some newTp)
            }

        loop (Map.empty, 0.0, Map.empty, None)
    )

    // Function to update trading params
    let updateTradingParams newParams =
        cacheAgent.Post(UpdateTradingParams newParams)

    // Function to get data from the cache
    let getCachedData key =
        cacheAgent.PostAndAsyncReply(fun reply -> Get(key, reply))

    let connectToWebSocket (uri: Uri) : Async<Result<ClientWebSocket, string>> =
        async {
            let wsClient = new ClientWebSocket()
            try
                do! wsClient.ConnectAsync(uri, CancellationToken.None) |> Async.AwaitTask
                return Ok wsClient
            with ex ->
                return Error $"Failed to connect to WebSocket: {ex.Message}"
        }

    let sendJsonMessage (wsClient: ClientWebSocket) (message: Message) : Async<Result<unit, string>> =
        async {
            let messageJson = JsonSerializer.Serialize(message)
            let messageBytes = Encoding.UTF8.GetBytes(messageJson)
            let segment = ArraySegment<byte>(messageBytes)
            try
                do! wsClient.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask
                return Ok ()
            with ex ->
                return Error $"Failed to send message: {ex.Message}"
        }

    let processMessage (message: string) : ProcessResult =
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        try
            let jsonDoc = JsonDocument.Parse(message)
            let root = jsonDoc.RootElement

            match root.ValueKind with
            | JsonValueKind.Array when root.GetArrayLength() > 0 ->
                let firstElement = root.[0]
                let ev = firstElement.GetProperty("ev").GetString()
                match ev with
                | "status" ->
                    NoAuthMessage
                | "XT" | "XQ" ->
                    match JsonSerializer.Deserialize<DataMessage[]>(message, options) with
                    | null | [||] ->
                        printfn "Failed to parse data messages."
                        NoAuthMessage
                    | dataMessages ->
                        dataMessages |> Array.iter (fun msg ->
                            let key = $"{msg.Pair}.{msg.ExchangeId}"
                            cacheAgent.Post(Update(key, msg))
                        )
                        NoAuthMessage
                | _ ->
                    printfn "Unknown event type: %s" ev
                    NoAuthMessage
            | _ ->
                printfn "Received empty or invalid message."
                NoAuthMessage
        with ex ->
            printfn "Error processing message: %s" ex.Message
            NoAuthMessage

    let receiveData (wsClient: ClientWebSocket) (subscriptionParameters: string) (cancellationToken: CancellationToken) : Async<unit> =
        let buffer = Array.zeroCreate<byte> 4096

        let rec receiveLoop () = async {
            match cancellationToken.IsCancellationRequested with
            | true ->
                try
                    do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopped by user", CancellationToken.None) |> Async.AwaitTask
                with _ -> ()
                return ()
            | false ->
                let segment = ArraySegment<byte>(buffer)
                let! result = wsClient.ReceiveAsync(segment, cancellationToken) |> Async.AwaitTask

                match result.MessageType with
                | WebSocketMessageType.Text ->
                    let message = Encoding.UTF8.GetString(buffer, 0, result.Count)
                    match processMessage message with
                    | AuthSuccess ->
                        let subscriptionMessage = { action = "subscribe"; params = subscriptionParameters }
                        let! subscribeResult = sendJsonMessage wsClient subscriptionMessage
                        match subscribeResult with
                        | Ok () ->
                            printfn "Subscribed to: %s" subscriptionParameters
                        | Error errMsg ->
                            printfn "Subscription error: %s" errMsg
                    | AuthFailed errMsg ->
                        printfn "Authentication failed: %s" errMsg
                        do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Authentication failed", CancellationToken.None) |> Async.AwaitTask
                    | NoAuthMessage ->
                        ()
                    return! receiveLoop ()

                | WebSocketMessageType.Close ->
                    match wsClient.State with
                    | WebSocketState.Open ->
                        do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask
                        return ()
                    | _ ->
                        return ()

                | _ ->
                    return! receiveLoop ()
        }

        receiveLoop ()

    let start (uri: Uri, apiKey: string, subscriptionParameters: string, cancellationToken: CancellationToken) =
        async {
            let! connectionResult = connectToWebSocket uri
            match connectionResult with
            | Ok wsClient ->
                let authMessage = { action = "auth"; params = apiKey }
                let! authResult = sendJsonMessage wsClient authMessage
                match authResult with
                | Ok () ->
                    do! receiveData wsClient subscriptionParameters cancellationToken
                | Error errMsg ->
                    printfn "Authentication message send failed: %s" errMsg
            | Error errMsg ->
                printfn "Connection failed: %s" errMsg
        }
