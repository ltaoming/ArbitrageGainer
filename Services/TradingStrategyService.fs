namespace Application

open Domain

module TradingStrategyService =
    open ArbitrageGainer.Core

    let saveAndSetCurrentStrategy (agent: TradingStrategyAgent) (dto: TradingStrategyDto) : Async<Result<string, TradingStrategyError>> =
        agent.SaveAndSetCurrentStrategy(dto)

    let getCurrentStrategy (agent: TradingStrategyAgent) : Async<Option<TradingStrategy>> =
        agent.GetCurrentStrategy()