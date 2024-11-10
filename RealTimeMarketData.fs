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
    type Message = { action: string; params: string }

    type StatusMessage = {
        [<JsonPropertyName("ev")>]
        Ev: string
        [<JsonPropertyName("status")>]
        Status: string
        [<JsonPropertyName("message")>]
        Message: string
    }

    // Discriminated union to represent processing results
    type ProcessResult =
        | AuthSuccess
        | AuthFailed of string
        | NoAuthMessage

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

    let processMessage (message: string) : ProcessResult =
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        let statusMessages = JsonSerializer.Deserialize<StatusMessage[]>(message, options)
        match statusMessages with
        | null -> 
            printfn "Failed to parse message."
            NoAuthMessage
        | [||] -> 
            printfn "Received empty message."
            NoAuthMessage
        | _ ->
            let mutable result = NoAuthMessage
            for msg in statusMessages do
                match msg.Ev with
                | "status" ->
                    match msg.Status with
                    | "auth_success" -> 
                        printfn "Authentication successful."
                        result <- AuthSuccess
                    | "auth_failed" -> 
                        printfn "Authentication failed: %s" msg.Message
                        result <- AuthFailed msg.Message
                    | _ -> 
                        printfn "Status: %s - %s" msg.Status msg.Message
                | "XT" | "XQ" -> 
                    // store the data into cache
                    cache.[msg.Ev] <- message
                    printfn "Updated data for %s: %s" msg.Ev message
                | _ -> 
                    printfn "Unknown event type: %s" msg.Ev
            result

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
                let processResult = processMessage message

                match processResult with
                | AuthSuccess ->
                    // Send subscription message
                    let! subscribeResult =
                        sendJsonMessage wsClient { 
                            action = "subscribe"; 
                            params = subscriptionParameters 
                        }

                    match subscribeResult with
                    | Ok () -> 
                        printfn "Subscribed to: %s" subscriptionParameters
                    | Error errMsg -> 
                        printfn "Subscription error: %s" errMsg

                | AuthFailed errMsg ->
                    // Handle authentication failure
                    printfn "Authentication failed: %s" errMsg
                    // Optionally, close the connection
                    do! wsClient.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, 
                            "Authentication failed", 
                            CancellationToken.None)
                        |> Async.AwaitTask

                | NoAuthMessage ->
                    // Do nothing or handle non-auth messages if needed
                    ()

                return! receiveLoop ()

            | WebSocketMessageType.Close ->
                printfn "WebSocket closed by server."
                do! wsClient.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Closing", 
                        CancellationToken.None)
                    |> Async.AwaitTask

            | _ -> 
                // Handle other message types if necessary
                return! receiveLoop ()
        }

        receiveLoop ()

    let start(uri: Uri, apiKey: string, subscriptionParameters: string) =
        async {
            let! connectionResult = connectToWebSocket uri
            match connectionResult with
            | Ok wsClient ->
                // Authenticate with Polygon
                let! authResult = sendJsonMessage wsClient { action = "auth"; params = apiKey }
                match authResult with
                | Ok () ->
                    // Start receiving data
                    do! receiveData wsClient subscriptionParameters
                | Error errMsg ->
                    printfn "Authentication message send failed: %s" errMsg
            | Error errMsg -> printfn "Connection failed: %s" errMsg
        }
