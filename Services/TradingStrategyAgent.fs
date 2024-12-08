namespace Application

open Domain
open ArbitrageGainer.Core
open Microsoft.Extensions.Logging
open System

type AgentMessage =
    | SaveStrategy of TradingStrategyDto * AsyncReplyChannel<Result<string, TradingStrategyError>>
    | GetStrategy of AsyncReplyChannel<Option<TradingStrategy>>
    | GetInitialInvestmentAmount of AsyncReplyChannel<Option<float>>
    | SetStartDateOfTrading of DateTime
    | GetStartDateOfTrading of AsyncReplyChannel<Option<DateTime>>

module TradingStrategyAgent =

    // The agent state: (Option<TradingStrategy>, Option<DateTime>)
    let private agentLoop (logger: ILogger) =
        MailboxProcessor.Start(fun inbox ->
            let rec loop (currentStrategy: Option<TradingStrategy>, startDateOfTrading: Option<DateTime>) = async {
                let! msg = inbox.Receive()
                match msg, currentStrategy, startDateOfTrading with
                | SaveStrategy (dto, reply), _, _ ->
                    match Validation.updateStrategyPure dto with
                    | Ok strategy ->
                        logger.LogInformation("Strategy updated successfully")
                        reply.Reply(Ok "Strategy updated successfully")
                        return! loop (Some strategy, startDateOfTrading)
                    | Error err ->
                        logger.LogError("Error updating strategy: {Error}", sprintf "%A" err)
                        reply.Reply(Error err)
                        return! loop (currentStrategy, startDateOfTrading)

                | GetStrategy reply, currentStrat, _ ->
                    reply.Reply(currentStrat)
                    return! loop (currentStrat, startDateOfTrading)

                | GetInitialInvestmentAmount reply, Some strategy, _ ->
                    let (InitialInvestment amount) = strategy.InitInvestment
                    reply.Reply(Some amount)
                    return! loop (Some strategy, startDateOfTrading)

                | GetInitialInvestmentAmount reply, None, _ ->
                    reply.Reply(None)
                    return! loop (None, startDateOfTrading)

                | SetStartDateOfTrading date, currentStrat, _ ->
                    return! loop (currentStrat, Some date)

                | GetStartDateOfTrading reply, currentStrat, startOpt ->
                    reply.Reply(startOpt)
                    return! loop (currentStrat, startOpt)
            }
            loop (None, None)
        )

    type TradingStrategyAgentAPI = {
        SaveAndSetCurrentStrategy: TradingStrategyDto -> Async<Result<string, TradingStrategyError>>
        GetCurrentStrategy: unit -> Async<Option<TradingStrategy>>
        GetInitialInvestmentAmount: unit -> Async<Option<float>>
        SetStartDateOfTrading: DateTime -> unit
        GetStartDateOfTrading: unit -> Async<Option<DateTime>>
    }

    let createTradingStrategyAgent (logger: ILogger) : TradingStrategyAgentAPI =
        let agent = agentLoop logger
        {
            SaveAndSetCurrentStrategy = fun dto ->
                agent.PostAndAsyncReply(fun reply -> SaveStrategy(dto, reply))
            GetCurrentStrategy = fun () ->
                agent.PostAndAsyncReply(GetStrategy)
            GetInitialInvestmentAmount = fun () ->
                agent.PostAndAsyncReply(GetInitialInvestmentAmount)
            SetStartDateOfTrading = fun date ->
                agent.Post(SetStartDateOfTrading date)
            GetStartDateOfTrading = fun () ->
                agent.PostAndAsyncReply(GetStartDateOfTrading)
        }

    // Alias for compatibility with existing code referencing TradingStrategyAgent
    type TradingStrategyAgent = TradingStrategyAgentAPI
