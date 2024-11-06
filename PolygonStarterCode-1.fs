module PolygonStarterCode1

open System
open System.Net
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Text
open DotNetEnv

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
        try
            let statusMessages = JsonSerializer.Deserialize<StatusMessage[]>(message, options)
            match statusMessages with
            | null -> printfn "No messages to process"
            | messages -> messages |> Array.iter (fun msg -> printfn "Event: %s, Status: %s" msg.Ev msg.Status)
        with
        | ex -> printfn "Failed to process message: %s" ex.Message

    let receiveData (wsClient: ClientWebSocket) =
        async {
            let buffer = ArraySegment(Array.zeroCreate 8192)
            let rec receiveLoop () =
                async {
                    let! result = wsClient.ReceiveAsync(buffer, CancellationToken.None) |> Async.AwaitTask
                    if result.MessageType = WebSocketMessageType.Close then
                        do! wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None) |> Async.AwaitTask
                    else
                        let message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count)
                        processMessage message
                        return! receiveLoop ()
                }
            receiveLoop ()
        }

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
                    do! receiveData wsClient
                | Error errMsg ->
                    printfn "%s" errMsg
            | Error errMsg -> printfn "%s" errMsg
        }