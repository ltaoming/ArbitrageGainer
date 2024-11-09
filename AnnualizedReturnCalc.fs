module ArbitrageGainer.AnnualizedReturnCalc

open System

type Result<'T> =
    | Ok of 'T
    | Error of string

type AnnualizedParam = {initInvest:float; cumPNL:float; durationOfYear:float}

let calculateDoY:float=
    (DateTime.Now - DateTime.Parse("10/24/2024")) / TimeSpan(365, 0, 0, 0)
    
let validateDoY (input: float) =
    match input > 0.0 with
    | false -> Error "Duration of Year should be larger than 0"
    | _ -> Ok input
    
let validatePNY (input: float) =
    match input > 0.0 with
    | false -> Error "PNY should be larger than 0"
    | _ -> Ok input
    
let validateInitInvest (input: float) =
    match input > 0.0 with
    | false -> Error "initial investment should be larger than 0"
    | _ -> Ok input
    
let validAnnualizedParamCheck (annualizedParam:AnnualizedParam) =
    let pnyResult = validatePNY annualizedParam.cumPNL
    let initInvestResult = validateInitInvest annualizedParam.initInvest
    let doyResult = validateDoY annualizedParam.durationOfYear
    match (pnyResult, initInvestResult, doyResult) with
    | (Ok _, Ok _, Ok _) -> Ok (pnyResult, initInvestResult, doyResult)
    | (Error errorMsg, _, _) -> Error errorMsg
    | (_, Error errorMsg, _) -> Error errorMsg
    | (_, _, Error errorMsg) -> Error errorMsg


let calculateAnnualizedReturn (annualizedParam:AnnualizedParam) =
    ((annualizedParam.cumPNL / annualizedParam.initInvest) ** (1.0 / annualizedParam.durationOfYear)) - 1.0
    
let annualizedReturnCalcHandler (initInvest:float) =
    let currDoY = calculateDoY
    let annualizedParam = {initInvest=initInvest; cumPNL=5.0; durationOfYear=currDoY}
    let validationResult = validAnnualizedParamCheck annualizedParam
    match validationResult with
    | Ok _ ->
        let result = calculateAnnualizedReturn annualizedParam
        string result
    | Error errorMsg -> errorMsg
    