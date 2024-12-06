namespace Application

open Domain
open Microsoft.Extensions.Logging
open System
open ArbitrageGainer.Core

type TradingStrategyAgent(logger: ILogger) =

    let agent = MailboxProcessor.Start(fun inbox ->
        let rec loop (currentStrategy: Option<TradingStrategy>, startDateOfTrading: Option<DateTime>) = async {
            let! msg = inbox.Receive()
            match msg with
            | SaveStrategy (dto, reply) ->
                match Validation.updateStrategyPure dto with
                | Ok strategy ->
                    logger.LogInformation("Strategy updated successfully")
                    reply.Reply(Ok "Strategy updated successfully")
                    return! loop (Some strategy, startDateOfTrading)
                | Error err ->
                    logger.LogError("Error updating strategy: {Error}", sprintf "%A" err)
                    reply.Reply(Error err)
                    return! loop (currentStrategy, startDateOfTrading)

            | GetStrategy reply ->
                reply.Reply(currentStrategy)
                return! loop (currentStrategy, startDateOfTrading)

            | GetInitialInvestmentAmount reply ->
                match currentStrategy with
                | Some strategy ->
                    let (InitialInvestment amount) = strategy.InitInvestment
                    reply.Reply(Some amount)
                | None ->
                    reply.Reply(None)
                return! loop (currentStrategy, startDateOfTrading)

            | SetStartDateOfTrading date ->
                return! loop (currentStrategy, Some date)

            | GetStartDateOfTrading reply ->
                reply.Reply(startDateOfTrading)
                return! loop (currentStrategy, startDateOfTrading)
        }

        // Initial state: no current strategy and no start date.
        loop (None, None)
    )

    member _.SaveAndSetCurrentStrategy(dto: TradingStrategyDto) =
        agent.PostAndAsyncReply(fun reply -> SaveStrategy (dto, reply))

    member _.GetCurrentStrategy() =
        agent.PostAndAsyncReply(fun reply -> GetStrategy reply)

    member _.GetInitialInvestmentAmount() =
        agent.PostAndAsyncReply(fun reply -> GetInitialInvestmentAmount reply)

    member _.SetStartDateOfTrading(date: DateTime) =
        agent.Post(SetStartDateOfTrading date)

    member _.GetStartDateOfTrading() =
        agent.PostAndAsyncReply(fun reply -> GetStartDateOfTrading reply)

and AgentMessage =
    | SaveStrategy of TradingStrategyDto * AsyncReplyChannel<Result<string, TradingStrategyError>>
    | GetStrategy of AsyncReplyChannel<Option<TradingStrategy>>
    | GetInitialInvestmentAmount of AsyncReplyChannel<Option<float>>
    | SetStartDateOfTrading of DateTime
    | GetStartDateOfTrading of AsyncReplyChannel<Option<DateTime>>