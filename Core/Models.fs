namespace ArbitrageGainer.Core

open MongoDB.Bson
open System

type CurrencyCount = CurrencyCount of int
type PriceSpread = PriceSpread of float
type TransactionValue = TransactionValue of float
type TradingValue = TradingValue of float
type InitialInvestment = InitialInvestment of float

// Validation Error Types
type ValidationError =
    | MissingField of string
    | NumberOfCurrenciesMustBePositive
    | MinimalPriceSpreadMustBePositive
    | MaximalTransactionValueMustBePositive
    | MaximalTradingValueMustBePositive
    | MaximalTransactionValueLessThanMinimalPriceSpread
    | InitialInvestmentMustBePositive

type TradingStrategy = {
    NumberOfCurrencies: CurrencyCount
    MinimalPriceSpread: PriceSpread
    MinTransactionProfit: TransactionValue
    MaximalTransactionValue: TransactionValue
    MaximalTradingValue: TradingValue
    InitInvestment: InitialInvestment
}

type TradingStrategyDto = {
    NumberOfCurrencies: CurrencyCount
    MinimalPriceSpread: PriceSpread
    MinTransactionProfit: TransactionValue
    MaximalTransactionValue: TransactionValue
    MaximalTradingValue: TradingValue
    InitInvestment: float
}

type HistoricalArbitrageOpportunities = {
    CurrencyPair: string
    NumOpportunities: int
    Timestamp: DateTime
}

type Order = {
    Id: BsonObjectId
    CurrencyPair: string
    OrderType: string
    OrderStatus: string
    OrderQuantity: decimal
    OrderPrice: decimal
    Timestamp: DateTime
}

type TradeRecord = {
    Id: BsonObjectId
    CurrencyPair: string
    OrderType: string
    Quantity: decimal
    Price: decimal
    Timestamp: DateTime
}

type PNLRecord = {
    Id: BsonObjectId
    CurrencyPair: string
    PNL: decimal
    Timestamp: DateTime
}

type TradingStrategyError =
    | ValidationErrors of ValidationError list
    | InvalidInputError of string
    | RepositoryError of string