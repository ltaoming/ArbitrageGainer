module RealTimeMarketData

open System
open System.Collections.Generic
open System.Net
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Text

// WebSocket-related function module
module PolygonWebSocket =
    type Message = { action: string; parameters: string }

    type StatusMessage = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("status")>]
        Status: string
        [<JsonPropertyName("message")>]
        Message: string
    }

    // Cache for storing the latest market data
    let cache = Dictionary<string, string>()

    let connectToWebSocket (uri: Uri) : Async<Result<ClientWebSocket, string>> =
        async {
            let wsClient = new ClientWebSocket()
            try
                do! Async.AwaitTask (wsClient.ConnectAsync(uri, CancellationToken.None))
                return Ok wsClient
            with
            | ex ->
                return Error $"Failed to connect to WebSocket: {ex.Message}"
        }

    let sendJsonMessage (wsClient: ClientWebSocket) message : Async<Result<unit, string>> =
        async {
            let messageJson = JsonSerializer.Serialize(message)
            let messageBytes = Encoding.UTF8.GetBytes(messageJson)
            let segment = new ArraySegment<byte>(messageBytes)
            try
                do! wsClient.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None)
                    |> Async.AwaitTask
                return Ok ()
            with
            | ex -> return Error $"Failed to send message: {ex.Message}"
        }

    let processMessage (message: string) =
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        let statusMessages = JsonSerializer.Deserialize<StatusMessage[]>(message, options)
        match statusMessages with
        | null -> 
            printfn "Failed to parse message."
            false
        | [||] -> 
            printfn "Received empty message."
            false
        | _ ->
            let mutable authSuccess = false
            for msg in statusMessages do
                match msg.Ev with
                | "status" ->
                    match msg.Status with
                    | "auth_success" -> 
                        printfn "Authentication successful."
                        authSuccess <- true
                    | "auth_failed" -> printfn "Authentication failed: %s" msg.Message
                    | _ -> printfn "Status: %s - %s" msg.Status msg.Message
                | "XT" | "XQ" -> 
                    // Check if the message has changed compared to the cache
                    if not (cache.ContainsKey(msg.Ev) && cache.[msg.Ev] = message) then
                        cache.[msg.Ev] <- message
                        printfn "Updated data for %s: %s" msg.Ev message
                | _ -> printfn "Unknown event type: %s" msg.Ev
            authSuccess

    let receiveData (wsClient: ClientWebSocket) (subscriptionParameters: string) : Async<unit> =
        let buffer = Array.zeroCreate 4096
        let rec receiveLoop () = async {
            let segment = new ArraySegment<byte>(buffer)
            let! result =
                wsClient.ReceiveAsync(segment, CancellationToken.None)
                |> Async.AwaitTask
            match result.MessageType with
            | WebSocketMessageType.Text ->
                let message = Encoding.UTF8.GetString(buffer, 0, result.Count)
                let authSuccess = processMessage message
                if authSuccess then
                    // Send subscription message
                    let! subscribeResult = sendJsonMessage wsClient { action = "subscribe"; parameters = subscriptionParameters }
                    match subscribeResult with
                    | Ok () -> printfn "Subscribed to: %s" subscriptionParameters
                    | Error errMsg -> printfn "%s" errMsg
                return! receiveLoop ()
            | WebSocketMessageType.Close ->
                printfn "WebSocket closed by server."
                do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None)
                    |> Async.AwaitTask
            | _ -> return! receiveLoop ()
        }
        receiveLoop ()

    let start(uri: Uri, apiKey: string, subscriptionParameters: string) =
        async {
            let! connectionResult = connectToWebSocket uri
            match connectionResult with
            | Ok wsClient ->
                // Authenticate with Polygon
                let! authResult = sendJsonMessage wsClient { action = "auth"; parameters = apiKey }
                match authResult with
                | Ok () ->
                    // Start receiving data
                    do! receiveData wsClient subscriptionParameters
                | Error errMsg ->
                    printfn "%s" errMsg
            | Error errMsg -> printfn "%s" errMsg
        }
