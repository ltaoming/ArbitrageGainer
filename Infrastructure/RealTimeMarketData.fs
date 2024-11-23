namespace RealTimeMarketData

module PolygonWebSocket =
    open System
    open System.Net.WebSockets
    open System.Text.Json
    open System.Text.Json.Serialization
    open System.Threading
    open System.Text

    type Message = { action: string; params: string }

    type StatusMessage = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("status")>]
        Status: string
        [<JsonPropertyName("message")>]
        Message: string
    }

    type DataMessageBase = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("pair")>]
        Pair: string
        // Add other fields as needed
    }

    // Discriminated union to represent processing results
    type ProcessResult =
        | AuthSuccess
        | AuthFailed of string
        | NoAuthMessage

    // Define messages that the cache agent can handle
    type CacheMessage =
        | Update of key: string * value: string
        | Get of key: string * reply: AsyncReplyChannel<Option<string>>

    // Define the Cache Agent using MailboxProcessor
    let cacheAgent = MailboxProcessor.Start(fun inbox ->
        let rec loop (cache: Map<string, string>) =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Update (key, value) ->
                    match cache.TryFind key with
                    | Some existingValue when existingValue = value ->
                        // Data is the same, do nothing
                        return! loop cache
                    | _ ->
                        // Data is different or not in cache, update cache
                        let updatedCache = cache.Add(key, value)
                        // Uncomment the following line to print the updated cache content
                        updatedCache |> Map.iter (fun k v -> printfn "  %s: %s" k v)
                        return! loop updatedCache
                | Get (key, reply) ->
                    // Retrieve the value for the given key
                    let value = cache.TryFind key
                    reply.Reply value
                    return! loop cache
            }
        loop Map.empty
    )

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

            match root.ValueKind, root.GetArrayLength() > 0 with
            | JsonValueKind.Array, true ->
                let firstElement = root.[0]
                let ev = firstElement.GetProperty("ev").GetString()
                match ev with
                | "status" ->
                    // Deserialize as StatusMessage[]
                    let statusMessages = JsonSerializer.Deserialize<StatusMessage[]>(message, options)
                    statusMessages
                    |> Array.fold (fun acc msg ->
                        match msg.Ev, msg.Status with
                        | "status", "auth_success" ->
                            printfn "Authentication successful."
                            AuthSuccess
                        | "status", "auth_failed" ->
                            printfn "Authentication failed: %s" msg.Message
                            AuthFailed msg.Message
                        | _ ->
                            acc
                    ) NoAuthMessage
                | "XT" | "XQ" ->
                    // Process data messages
                    match JsonSerializer.Deserialize<DataMessageBase[]>(message, options) with
                    | null | [||] ->
                        printfn "Failed to parse data messages."
                        NoAuthMessage
                    | dataMessages ->
                        dataMessages |> Array.iter (fun msg ->
                            let key = $"{msg.Ev}.{msg.Pair}" // e.g., "XT.BTC-USD"
                            cacheAgent.Post(Update(key, message))
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
                let message = Encoding.UTF8.GetString(buffer, 0, result.Count)
                match processMessage message with
                | AuthSuccess ->
                    // Send subscription message
                    let subscriptionMessage = { action = "subscribe"; params = subscriptionParameters }
                    let! subscribeResult = sendJsonMessage wsClient subscriptionMessage
                    match subscribeResult with
                    | Ok () -> printfn "Subscribed to: %s" subscriptionParameters
                    | Error errMsg -> printfn "Subscription error: %s" errMsg
                | AuthFailed errMsg ->
                    // Handle authentication failure
                    printfn "Authentication failed: %s" errMsg
                    do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Authentication failed", CancellationToken.None) |> Async.AwaitTask
                | NoAuthMessage ->
                    () // No action needed

                return! receiveLoop ()

            | WebSocketMessageType.Close ->
                printfn "WebSocket closed by server."
                do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask
                return ()

            | _ ->
                // Ignore other message types and continue receiving
                return! receiveLoop ()
        }

        receiveLoop ()

    let start (uri: Uri, apiKey: string, subscriptionParameters: string) =
        async {
            let! connectionResult = connectToWebSocket uri
            match connectionResult with
            | Ok wsClient ->
                // Authenticate with Polygon
                let authMessage = { action = "auth"; params = apiKey }
                let! authResult = sendJsonMessage wsClient authMessage
                match authResult with
                | Ok () ->
                    // Start receiving data
                    do! receiveData wsClient subscriptionParameters
                | Error errMsg ->
                    printfn "Authentication message send failed: %s" errMsg
            | Error errMsg ->
                printfn "Connection failed: %s" errMsg
        }
