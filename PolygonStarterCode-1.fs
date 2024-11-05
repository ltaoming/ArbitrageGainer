module PolygonStarterCode1

open System
open System.Net
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Text
open DotNetEnv

// TODO:
// Utilize Result types for handling errors
// Introduce error handling
// Define Polygon message types and introduce a message processing function
// Improve encapsulation in the code

// Define a function to connect to the WebSocket
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
        | null -> printfn "Failed to parse message."
        | [||] -> printfn "Received empty message."
        | _ ->
            for msg in statusMessages do
                match msg.Ev with
                | "status" ->
                    match msg.Status with
                    | "auth_success" -> printfn "Authentication successful."
                    | "auth_failed" -> printfn "Authentication failed: %s" msg.Message
                    | _ -> printfn "Status: %s - %s" msg.Status msg.Message
                | "XT" -> 
                    // Handle trade messages
                    printfn "Received trade message: %s" message
                | "XQ" -> 
                    // Handle quote messages
                    printfn "Received quote message: %s" message
                | _ -> printfn "Unknown event type: %s" msg.Ev

    let receiveData (wsClient: ClientWebSocket) : Async<unit> =
        let buffer = Array.zeroCreate 4096
        let rec receiveLoop () = async {
            let segment = new ArraySegment<byte>(buffer)
            let! result =
                wsClient.ReceiveAsync(segment, CancellationToken.None)
                |> Async.AwaitTask
            match result.MessageType with
            | WebSocketMessageType.Text ->
                let message = Encoding.UTF8.GetString(buffer, 0, result.Count)
                processMessage message
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
                let! authResult = sendJsonMessage wsClient { action = "auth"; params = apiKey }
                match authResult with
                | Ok () ->
                    // Start receiving data
                    do! receiveData wsClient
                | Error errMsg ->
                    printfn "%s" errMsg
            | Error errMsg -> printfn "%s" errMsg
        }

[<EntryPoint>]
let main args =
    Env.Load() |> ignore
    let apiKey = Environment.GetEnvironmentVariable("API_KEY")
    let uri = Uri("wss://socket.polygon.io/crypto")
    let subscriptionParameters = "XT.BTC-USD"
    PolygonWebSocket.start (uri, apiKey, subscriptionParameters) |> Async.RunSynchronously
    0