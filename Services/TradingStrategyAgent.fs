namespace Application

open Domain
open Microsoft.Extensions.Logging

type TradingStrategyAgent(logger: ILogger) =

    let agent = MailboxProcessor.Start(fun inbox ->
        let rec loop (currentStrategy: Option<TradingStrategy>) = async {
            let! msg = inbox.Receive()
            match msg with
            | SaveStrategy (dto, reply) ->
                match Validation.updateStrategyPure dto with
                | Ok strategy ->
                    logger.LogInformation("Strategy updated successfully")
                    reply.Reply(Ok "Strategy updated successfully")
                    return! loop (Some strategy)
                | Error err ->
                    logger.LogError("Error updating strategy: {Error}", sprintf "%A" err)
                    reply.Reply(Error err)
                    return! loop currentStrategy
            | GetStrategy reply ->
                reply.Reply(currentStrategy)
                return! loop currentStrategy
        }
        loop None
    )

    member _.SaveAndSetCurrentStrategy(dto: TradingStrategyDto) =
        agent.PostAndAsyncReply(fun reply -> SaveStrategy (dto, reply))

    member _.GetCurrentStrategy() =
        agent.PostAndAsyncReply(fun reply -> GetStrategy reply)

and AgentMessage =
    | SaveStrategy of TradingStrategyDto * AsyncReplyChannel<Result<string, TradingStrategyError>>
    | GetStrategy of AsyncReplyChannel<Option<TradingStrategy>>