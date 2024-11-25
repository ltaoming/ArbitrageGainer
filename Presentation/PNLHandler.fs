namespace Presentation.PNLHandler

module PNLHandler =
    open Giraffe
    open Microsoft.AspNetCore.Http
    open FSharp.Control.Tasks
    open Domain
    open Services.PNLCalculation
    open System

    type PNLThresholdDto = {
        Threshold: decimal
    }

    type PNLStatusDto = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
    }

    type ResponseDto = {
        Status: string
    }

    type HistoricalPNLResponse = {
        TotalPNL: decimal
    }

    let setPNLThresholdHandler : HttpHandler =
        fun next ctx ->
            task {
                let! dto = ctx.BindJsonAsync<PNLThresholdDto>()
                match setPNLThreshold dto.Threshold with
                | Ok () -> return! json { Status = "Threshold updated successfully" } next ctx
                | Error (InvalidPNLThreshold msg) -> return! RequestErrors.BAD_REQUEST msg next ctx
                | Error _ -> return! ServerErrors.INTERNAL_ERROR "An error occurred" next ctx
            }

    let getCurrentPNLHandler : HttpHandler =
        fun next ctx ->
            task {
                let status = getCurrentPNLStatus ()
                let dto = {
                    CurrentPNL = status.CurrentPNL
                    Threshold = status.Threshold
                    ThresholdReached = status.ThresholdReached
                }
                return! json dto next ctx
            }

    let getHistoricalPNLHandler : HttpHandler =
        fun next ctx ->
            task {
                let startDateStr = ctx.Request.Query.["startDate"].ToString()
                let endDateStr = ctx.Request.Query.["endDate"].ToString()
                match DateTime.TryParse(startDateStr), DateTime.TryParse(endDateStr) with
                | (true, startDate), (true, endDate) ->
                    let totalPNL = getHistoricalPNL startDate endDate
                    return! json { TotalPNL = totalPNL } next ctx
                | _ ->
                    return! RequestErrors.BAD_REQUEST "Invalid dates provided" next ctx
            }

    let PNLWebApp : HttpHandler =
        choose [
            POST >=> route "/set-pnl-threshold" >=> setPNLThresholdHandler
            GET >=> route "/current-pnl" >=> getCurrentPNLHandler
            GET >=> route "/historical-pnl" >=> getHistoricalPNLHandler
        ]
