module ArbitrageGainer.AnnualizedReturnCalc

open System
open Giraffe
open System.Text.Json
open System.Text.Json.Serialization

type AnnualizedParam = {initInvest:float; cumPNL:float; durationOfYear:float}

type Response = {status:string; message:string}

let calculateDoY (dateNow: DateTime):float=
    (dateNow - DateTime.Parse("10/24/2024")) / TimeSpan(365, 0, 0, 0)
    
let validateDoY (input: float) =
    match input > 0.0 with
    | false -> Error "Duration of Year should be larger than 0"
    | _ -> Ok input
    
let validatePNL (input: float) =
    match input > 0.0 with
    | false -> Error "PNY should be larger than 0"
    | _ -> Ok input
    
let validateInitInvest (input: float) =
    match input > 0.0 with
    | false -> Error "initial investment should be larger than 0"
    | _ -> Ok input
    
let validAnnualizedParamCheck (annualizedParam:AnnualizedParam) =
    let pnyResult = validatePNL annualizedParam.cumPNL
    let initInvestResult = validateInitInvest annualizedParam.initInvest
    let doyResult = validateDoY annualizedParam.durationOfYear
    match (pnyResult, initInvestResult, doyResult) with
    | (Ok _, Ok _, Ok _) -> Ok (pnyResult, initInvestResult, doyResult)
    | (Error errorMsg, _, _) -> Error errorMsg
    | (_, Error errorMsg, _) -> Error errorMsg
    | (_, _, Error errorMsg) -> Error errorMsg


let calculateAnnualizedReturn (annualizedParam:AnnualizedParam) =
    ((annualizedParam.cumPNL / annualizedParam.initInvest) ** (1.0 / annualizedParam.durationOfYear)) - 1.0
    
    
// let errorCheck (validationResult:Result<'T, string>) (annualizedParam: AnnualizedParam)=
//     match validationResult with
//     | Ok _ ->
//         let result = calculateAnnualizedReturn annualizedParam
//         Ok (string result)
//     | Error errorMsg -> Error errorMsg
    
let annualizedReturnCalc (initInvest:float) =
    let annualizedParam = {initInvest=initInvest; cumPNL=5.0; durationOfYear=calculateDoY DateTime.Now}
    let validationResult = annualizedParam |> validAnnualizedParamCheck
    match validationResult with
    | Ok _ ->
        let result = calculateAnnualizedReturn annualizedParam
        Ok (string result)
    | Error errorMsg -> Error errorMsg

let getAnnualizedReturnHandler: HttpHandler =
    fun next ctx ->
        let initInvestStr = ctx.Request.Query.["initInvest"].ToString()
        match Double.TryParse(initInvestStr) with
        | (true, initInvest) ->
            match annualizedReturnCalc initInvest with
            | Ok result ->
                json {status="succeed"; message=result} next ctx
            | Error errorMessage ->
                RequestErrors.BAD_REQUEST {status="failed"; message=errorMessage} next ctx
        | _ ->
            RequestErrors.badRequest (json {status="failed"; message="Invalid initInvest parameter"}) next ctx
    

type AnnualizedReturnApp () =
    member _.WebApp =
        choose [
            GET >=> route "/annualized-return" >=> getAnnualizedReturnHandler
        ]
