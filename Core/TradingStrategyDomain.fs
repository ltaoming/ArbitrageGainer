namespace Domain

// Domain Types
type CurrencyCount = CurrencyCount of int
type PriceSpread = PriceSpread of float
type TransactionValue = TransactionValue of float
type TradingValue = TradingValue of float

// Validation Error Types
type ValidationError =
    | MissingField of string
    | NumberOfCurrenciesMustBePositive
    | MinimalPriceSpreadMustBePositive
    | MaximalTransactionValueMustBePositive
    | MaximalTradingValueMustBePositive
    | MaximalTransactionValueLessThanMinimalPriceSpread

// Domain Model
type TradingStrategy = {
    NumberOfCurrencies: CurrencyCount
    MinimalPriceSpread: PriceSpread
    MaximalTransactionValue: TransactionValue
    MaximalTradingValue: TradingValue
}

// DTO for Deserialization
type TradingStrategyDto = {
    NumberOfCurrencies: int option
    MinimalPriceSpread: float option
    MaximalTransactionValue: float option
    MaximalTradingValue: float option
}

// Error Type for Application
type TradingStrategyError =
    | ValidationErrors of ValidationError list
    | InvalidInputError of string
    | RepositoryError of string // General error for repository operations

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
    let updateStrategyPure (dto: TradingStrategyDto) : Result<TradingStrategy, TradingStrategyError> =
        result {
            let! currencyCount =
                match dto.NumberOfCurrencies with
                | Some v when v > 0 -> Ok (CurrencyCount v)
                | Some _ -> Error (ValidationErrors [NumberOfCurrenciesMustBePositive])
                | None -> Error (ValidationErrors [MissingField "NumberOfCurrencies"])

            let! priceSpread =
                match dto.MinimalPriceSpread with
                | Some v when v > 0.0 -> Ok (PriceSpread v)
                | Some _ -> Error (ValidationErrors [MinimalPriceSpreadMustBePositive])
                | None -> Error (ValidationErrors [MissingField "MinimalPriceSpread"])

            let! transactionValue =
                match dto.MaximalTransactionValue with
                | Some v when v > 0.0 -> Ok (TransactionValue v)
                | Some _ -> Error (ValidationErrors [MaximalTransactionValueMustBePositive])
                | None -> Error (ValidationErrors [MissingField "MaximalTransactionValue"])

            let! tradingValue =
                match dto.MaximalTradingValue with
                | Some v when v > 0.0 -> Ok (TradingValue v)
                | Some _ -> Error (ValidationErrors [MaximalTradingValueMustBePositive])
                | None -> Error (ValidationErrors [MissingField "MaximalTradingValue"])

            let (TransactionValue txValue) = transactionValue
            let (PriceSpread psValue) = priceSpread

            do!
                match txValue >= psValue with
                | true -> Ok ()
                | false -> Error (ValidationErrors [MaximalTransactionValueLessThanMinimalPriceSpread])

            return {
                NumberOfCurrencies = currencyCount
                MinimalPriceSpread = priceSpread
                MaximalTransactionValue = transactionValue
                MaximalTradingValue = tradingValue
            }
        }

// Repository Interface
type ITradingStrategyRepository =
    abstract member Save : TradingStrategy -> Result<unit, TradingStrategyError>
    abstract member Load : unit -> Result<TradingStrategy option, TradingStrategyError>
