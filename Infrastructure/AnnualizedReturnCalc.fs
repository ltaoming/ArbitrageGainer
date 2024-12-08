module ArbitrageGainer.AnnualizedReturnCalc

open System
open Giraffe
open System.Text.Json
open System.Text.Json.Serialization
open Application
open Application.TradingStrategyAgent // Access TradingStrategyAgent
open Services.PNLCalculation
open Microsoft.AspNetCore.Http

type AnnualizedParam = { initInvest:float; cumPNL:float; durationOfYear:float }

type Response = { status:string; message:string }

let calculateDoY (startDate: DateTime) (dateNow: DateTime):float =
    let duration = dateNow - startDate
    duration.TotalDays / 365.0

let validateDoY (input: float) =
    match input > 0.0 with
    | false -> Error "Duration of Year should be larger than 0"
    | _ -> Ok input

let validatePNL (input: float) =
    match input >= 0.0 with
    | false -> Error "PNL should be non-negative"
    | _ -> Ok input

let validateInitInvest (input: float) =
    match input > 0.0 with
    | false -> Error "Initial investment should be larger than 0"
    | _ -> Ok input

let validAnnualizedParamCheck (annualizedParam:AnnualizedParam) =
    let pnlResult = validatePNL annualizedParam.cumPNL
    let initInvestResult = validateInitInvest annualizedParam.initInvest
    let doyResult = validateDoY annualizedParam.durationOfYear
    match (pnlResult, initInvestResult, doyResult) with
    | (Ok _, Ok _, Ok _) -> Ok ()
    | (Error errorMsg, _, _) -> Error errorMsg
    | (_, Error errorMsg, _) -> Error errorMsg
    | (_, _, Error errorMsg) -> Error errorMsg

let calculateAnnualizedReturn (annualizedParam:AnnualizedParam) =
    ((annualizedParam.cumPNL / annualizedParam.initInvest) ** (1.0 / annualizedParam.durationOfYear)) - 1.0

let annualizedReturnCalc (agent: TradingStrategyAgent) =
    async {
        // Get initial investment amount
        let! initInvestOpt = agent.GetInitialInvestmentAmount()
        match initInvestOpt with
        | Some initInvest ->
            // Get cumulative PNL
            let! cumPNL = Services.PNLCalculation.getCumulativePNL()
            // Get start date of trading
            let! startDateOpt = agent.GetStartDateOfTrading()
            match startDateOpt with
            | Some startDate ->
                let durationOfYear = calculateDoY startDate DateTime.Now
                let annualizedParam = { initInvest=initInvest; cumPNL=cumPNL |> float; durationOfYear=durationOfYear }
                let validationResult = annualizedParam |> validAnnualizedParamCheck
                match validationResult with
                | Ok () ->
                    let result = calculateAnnualizedReturn annualizedParam
                    return Ok (string result)
                | Error errorMsg -> return Error errorMsg
            | None ->
                return Error "Trading has not been started yet"
        | None ->
            return Error "Initial investment amount not set"
    }

let getAnnualizedReturnHandler (agent: TradingStrategyAgent): HttpHandler =
    fun next ctx ->
        task {
            let! result = annualizedReturnCalc agent
            match result with
            | Ok annualizedReturn ->
                return! json { status="succeed"; message=annualizedReturn } next ctx
            | Error errorMessage ->
                return! RequestErrors.BAD_REQUEST { status="failed"; message=errorMessage } next ctx
        }

type AnnualizedReturnApp (agent: TradingStrategyAgent) =
    member _.WebApp =
        choose [
            GET >=> route "/annualized-return" >=> getAnnualizedReturnHandler agent
        ]