namespace Presentation.PNLHandler

module PNLHandler =
    open Giraffe
    open Microsoft.AspNetCore.Http
    open FSharp.Control.Tasks
    open Domain
    open Services.PNLCalculation
    open System

    // DTO to set a PNL Threshold
    type PNLThresholdDto = {
        Threshold: decimal
    }

    // DTO to return current PNL status including threshold information
    type PNLStatusDto = {
        CurrentPNL: decimal
        Threshold: decimal option
        ThresholdReached: bool
    }

    // DTO to return generic response
    type ResponseDto = {
        Status: string
    }

    // DTO to return historical PNL result
    type HistoricalPNLResponse = {
        TotalPNL: decimal
    }

    /// <summary>
    /// POST /set-pnl-threshold
    /// Sets or resets the P&L threshold.
    /// If threshold > 0: sets a new threshold at which trading stops and a notification email is sent.
    /// If threshold = 0: resets the threshold, effectively removing it.
    /// </summary>
    let setPNLThresholdHandler : HttpHandler =
        fun next ctx ->
            task {
                let! dto = ctx.BindJsonAsync<PNLThresholdDto>()
                let! result = setPNLThreshold dto.Threshold
                match result with
                | Ok () -> return! json { Status = "Threshold updated successfully" } next ctx
                | Error (InvalidPNLThreshold msg) -> return! RequestErrors.BAD_REQUEST msg next ctx
            }

    /// <summary>
    /// GET /current-pnl
    /// Retrieves the current P&L status including whether the threshold was reached.
    /// If threshold reached, trading is stopped and an email notification has been sent.
    /// </summary>
    let getCurrentPNLHandler : HttpHandler =
        fun next ctx ->
            task {
                let! status = getCurrentPNLStatus ()
                let dto = {
                    CurrentPNL = status.CurrentPNL
                    Threshold = status.Threshold
                    ThresholdReached = status.ThresholdReached
                }
                return! json dto next ctx
            }

    /// <summary>
    /// GET /historical-pnl?startDate={}&endDate={}
    /// On-demand P&L analysis: user provides a start and end date.
    /// The system returns the historical P&L over that period, allowing the user to analyze past performance.
    /// </summary>
    let getHistoricalPNLHandler : HttpHandler =
        fun next ctx ->
            task {
                let startDateStr = ctx.Request.Query.["startDate"].ToString()
                let endDateStr = ctx.Request.Query.["endDate"].ToString()
                match DateTime.TryParse(startDateStr), DateTime.TryParse(endDateStr) with
                | (true, startDate), (true, endDate) ->
                    let! totalPNL = getHistoricalPNL startDate endDate
                    return! json { TotalPNL = totalPNL } next ctx
                | _ ->
                    return! RequestErrors.BAD_REQUEST "Invalid dates provided" next ctx
            }

    /// PNLWebApp aggregates all the P&L related endpoints:
    /// - POST /set-pnl-threshold
    /// - GET /current-pnl
    /// - GET /historical-pnl (with startDate and endDate)
    ///
    /// These endpoints satisfy the requirement to manage P&L thresholds and perform on-demand P&L analysis.
    let PNLWebApp : HttpHandler =
        choose [
            POST >=> route "/set-pnl-threshold" >=> setPNLThresholdHandler
            GET >=> route "/current-pnl" >=> getCurrentPNLHandler
            GET >=> route "/historical-pnl" >=> getHistoricalPNLHandler
        ]