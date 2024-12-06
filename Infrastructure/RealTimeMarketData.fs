namespace RealTimeMarketData

open System
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Text
open TradingAlgorithm
open Infrastructure.RealTimeTradingLogic

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

    type ProcessResult =
        | AuthSuccess
        | AuthFailed of string
        | NoAuthMessage

    type CacheMessage =
        | Update of key: string * value: DataMessage
        | Get of key: string * reply: AsyncReplyChannel<Option<DataMessage>>
        | GetAll of reply: AsyncReplyChannel<Map<string, DataMessage>>

    let cacheAgent = MailboxProcessor.Start(fun inbox ->
        let rec loop (cache: Map<string, DataMessage>, cumulativeTradingValue: float, executedArbitrage: Map<string, DateTime>) =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Update (key, value) ->
                    let updatedCache = cache.Add(key, value)
                    let (finalCache, newCumulativeTradingValue, newExecutedArbitrage) =
                        processMarketData updatedCache cumulativeTradingValue executedArbitrage
                    return! loop (finalCache, newCumulativeTradingValue, newExecutedArbitrage)

                | Get (key, reply) ->
                    let value = cache.TryFind key
                    reply.Reply value
                    return! loop (cache, cumulativeTradingValue, executedArbitrage)

                | GetAll reply ->
                    reply.Reply cache
                    return! loop (cache, cumulativeTradingValue, executedArbitrage)
            }
        loop (Map.empty, 0.0, Map.empty)
    )

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

            match root.ValueKind, (if root.ValueKind = JsonValueKind.Array then root.GetArrayLength() > 0 else false) with
            | JsonValueKind.Array, true ->
                let firstElement = root.[0]
                let ev = firstElement.GetProperty("ev").GetString()
                match ev with
                | "status" ->
                    let status = firstElement.GetProperty("status").GetString()
                    match status with
                    | "connected" -> AuthSuccess
                    | _ -> NoAuthMessage
                | "XQ" | "XT" ->
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

    let receiveData (wsClient: ClientWebSocket) (subscriptionParameters: string) : Async<unit> =
        let buffer = Array.zeroCreate<byte> 4096
        let rec receiveLoop () = async {
            let segment = ArraySegment<byte>(buffer)
            let! result = wsClient.ReceiveAsync(segment, CancellationToken.None) |> Async.AwaitTask

            match result.MessageType with
            | WebSocketMessageType.Text ->
                let message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count)
                match processMessage message with
                | AuthSuccess ->
                    let subscriptionMessage = { action = "subscribe"; params = subscriptionParameters }
                    let! subscribeResult = sendJsonMessage wsClient subscriptionMessage
                    match subscribeResult with
                    | Ok () -> printfn "Subscribed to: %s" subscriptionParameters
                    | Error errMsg -> printfn "Subscription error: %s" errMsg
                | AuthFailed errMsg ->
                    printfn "Authentication failed: %s" errMsg
                    do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Authentication failed", CancellationToken.None) |> Async.AwaitTask
                | NoAuthMessage ->
                    ()
                return! receiveLoop ()

            | WebSocketMessageType.Close ->
                printfn "WebSocket closed by server."
                do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask
                return ()

            | _ ->
                return! receiveLoop ()
        }
        receiveLoop ()

    let start (uri: Uri, apiKey: string, subscriptionParameters: string) =
        async {
            let! connectionResult = connectToWebSocket uri
            match connectionResult with
            | Ok wsClient ->
                let authMessage = { action = "auth"; params = apiKey }
                let! authResult = sendJsonMessage wsClient authMessage
                match authResult with
                | Ok () ->
                    do! receiveData wsClient subscriptionParameters
                | Error errMsg ->
                    printfn "Authentication message send failed: %s" errMsg
            | Error errMsg ->
                printfn "Connection failed: %s" errMsg
        }