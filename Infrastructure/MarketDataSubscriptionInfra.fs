namespace MarketDataInfra

open Giraffe
open MarketDataService

module StartTradingHandlers =
    
    let startTradingHandler : HttpHandler =
        fun next ctx ->
            task {
                let apiKey = "BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e" // Replace with your actual API key
                TradingService.startTrading(apiKey)
                return! text "Started trading." next ctx
            }


