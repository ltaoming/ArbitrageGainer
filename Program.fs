module Program

open Suave
open TradingStrategy

[<EntryPoint>]
let main argv =
    // start Suave Web Server
    startWebServer defaultConfig app
    0
