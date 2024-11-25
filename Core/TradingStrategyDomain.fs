namespace Domain

// Domain Types
type CurrencyCount = CurrencyCount of int
type PriceSpread = PriceSpread of float
type TransactionValue = TransactionValue of float
type TradingValue = TradingValue of float
type InitialInvestment = InitialInvestment of float

// // Validation Error Types
// type ValidationError =
//     | MissingField of string
//     | NumberOfCurrenciesMustBePositive
//     | MinimalPriceSpreadMustBePositive
//     | MaximalTransactionValueMustBePositive
//     | MaximalTradingValueMustBePositive
//     | MaximalTransactionValueLessThanMinimalPriceSpread

// // Domain Model
// type TradingStrategy = {
//     NumberOfCurrencies: CurrencyCount
//     MinimalPriceSpread: PriceSpread
//     MaximalTransactionValue: TransactionValue
//     MaximalTradingValue: TradingValue
// }

// DTO for Deserialization
// type TradingStrategyDto = {
//     NumberOfCurrencies: int option
//     MinimalPriceSpread: float option
//     MaximalTransactionValue: float option
//     MaximalTradingValue: float option
// }

// Error Type for Application
// type TradingStrategyError =
//     | ValidationErrors of ValidationError list
//     | InvalidInputError of string
//     | RepositoryError of string // General error for repository operations

// Result Computation Expression
module ResultBuilder =
    type ResultBuilder() =
        member _.Bind(m, f) =
            match m with
            | Ok x -> f x
            | Error e -> Error e
        member _.Return(x) = Ok x

    let result = ResultBuilder()

open ResultBuilder

// Validation Function
module Validation =
    open Domain
    open ArbitrageGainer.Core

    let updateStrategyPure (dto: TradingStrategyDto) : Result<TradingStrategy, TradingStrategyError> =
        result {
            let! currencyCount =
                match dto.NumberOfCurrencies with
                | CurrencyCount v when v > 0 -> Ok (CurrencyCount v)
                | CurrencyCount _ -> Error (TradingStrategyError.ValidationErrors [NumberOfCurrenciesMustBePositive])

            let! priceSpread =
                match dto.MinimalPriceSpread with
                | PriceSpread v when v > 0.0 -> Ok (PriceSpread v)
                | PriceSpread _ -> Error (TradingStrategyError.ValidationErrors [MinimalPriceSpreadMustBePositive])

            let! transactionValue =
                match dto.MaximalTransactionValue with
                | TransactionValue v when v > 0.0 -> Ok (TransactionValue v)
                | TransactionValue _ -> Error (TradingStrategyError.ValidationErrors [MaximalTransactionValueMustBePositive])

            let! tradingValue =
                match dto.MaximalTradingValue with
                | TradingValue v when v > 0.0 -> Ok (TradingValue v)
                | TradingValue _ -> Error (TradingStrategyError.ValidationErrors [MaximalTradingValueMustBePositive])

            let! initialInvestment =
                match dto.InitInvestment with
                | v when v > 0.0 -> Ok (InitialInvestment v)
                | _ -> Error (TradingStrategyError.ValidationErrors [InitialInvestmentMustBePositive])
                
            let (TransactionValue txValue) = transactionValue
            let (PriceSpread psValue) = priceSpread

            do!
                match txValue >= psValue with
                | true -> Ok ()
                | false -> Error (TradingStrategyError.ValidationErrors [MaximalTransactionValueLessThanMinimalPriceSpread])

            return {
                NumberOfCurrencies = currencyCount
                MinimalPriceSpread = priceSpread
                MinTransactionProfit = dto.MinTransactionProfit
                MaximalTransactionValue = transactionValue
                MaximalTradingValue = tradingValue
                InitInvestment = initialInvestment
            }
        }
