namespace Infrastructure

open System

type TradingParams = {
    MinimalPriceSpread: float
    MinimalTransactionProfit: float
    MaximalTransactionValue: float
    MaximalTradingValue: float
}

type ParametersMessage =
    | GetParams of AsyncReplyChannel<TradingParams>
    | SetParams of TradingParams * AsyncReplyChannel<unit>

module TradingParametersAgent =

    let initialParams = {
        MinimalPriceSpread = 0.05
        MinimalTransactionProfit = 5.0
        MaximalTransactionValue = 2000.0
        MaximalTradingValue = 5000.0
    }

    let paramsAgent =
        MailboxProcessor.Start(fun inbox ->
            let rec loop currentParams = async {
                let! msg = inbox.Receive()
                match msg with
                | GetParams reply ->
                    reply.Reply(currentParams)
                    return! loop currentParams
                | SetParams (newParams, reply) ->
                    reply.Reply(())
                    return! loop newParams
            }
            loop initialParams
        )

    let getParameters () : TradingParams =
        paramsAgent.PostAndReply(GetParams)

    let setParameters (newParams: TradingParams) : unit =
        paramsAgent.PostAndReply(fun reply -> SetParams(newParams, reply))