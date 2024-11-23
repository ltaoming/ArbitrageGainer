namespace Application

open Microsoft.Extensions.Logging 
open Domain
open Infrastructure.FileRepository

module TradingStrategyService =
    let saveAndSetCurrentStrategy (logger: ILogger) (repository: TradingStrategyRepository) (dto: TradingStrategyDto) : Result<string, TradingStrategyError> =
        Validation.updateStrategyPure dto
        |> Result.bind (fun strategy ->
            repository.Save(strategy)
            |> Result.map (fun _ -> "Strategy updated successfully"))
        |> function
            | Ok message -> Ok message
            | Error err ->
                match err with
                | ValidationErrors errs ->
                    errs |> List.iter (fun e ->
                        match e with
                        | MissingField msg -> logger.LogError("Validation error: {Message}", msg)
                        | NumberOfCurrenciesMustBePositive -> logger.LogError("Validation error: NumberOfCurrencies must be positive")
                        | MinimalPriceSpreadMustBePositive -> logger.LogError("Validation error: MinimalPriceSpread must be positive")
                        | MaximalTransactionValueMustBePositive -> logger.LogError("Validation error: MaximalTransactionValue must be positive")
                        | MaximalTradingValueMustBePositive -> logger.LogError("Validation error: MaximalTradingValue must be positive")
                        | MaximalTransactionValueLessThanMinimalPriceSpread -> logger.LogError("Validation error: MaximalTransactionValue must be greater than or equal to MinimalPriceSpread")
                    )
                | RepositoryError msg -> logger.LogError("Repository error: {Message}", msg)
                | InvalidInputError msg -> logger.LogError("Invalid input error: {Message}", msg)
                Error err
