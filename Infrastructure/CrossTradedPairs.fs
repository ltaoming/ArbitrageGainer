module Infrastructure.CrossTradePairApi

open System.Net.Http
open FSharp.Control.Tasks

let fetchExchangeData (url: string) : Async<Result<string, string>> = async {
    use client = new HttpClient()
    try
        let! response = client.GetStringAsync(url) |> Async.AwaitTask
        return Ok response
    with ex ->
        return Error ex.Message
}