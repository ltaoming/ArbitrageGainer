module Notification

open EmailSender

let notifyUserOfOrderStatusUpdate (orderId: string) (orderStatus: string) =
    let emailBody = sprintf "Order %s updated: %s" orderId orderStatus
    let emailSubject = "Order Status Changed"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore

let notifyUserOfPLThresholdReached (threshold: decimal) =
    let emailBody = sprintf "P&L threshold reached: %M" threshold
    let emailSubject = "P&L Threshold has been reached"
    EmailSender.sendEmail "your-email@gmail.com" "recipient-email@example.com" emailSubject emailBody |> ignore
