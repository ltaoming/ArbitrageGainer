module Presentation.TestEmailHandler

open Giraffe
open EmailSender
open Microsoft.AspNetCore.Http
open System

let testEmailHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let from = "mlc2455213223@gmail.com" // 替换为您的发送邮箱
            let recipient = "lechuanm@andrew.cmu.edu" // 替换为您要接收测试邮件的邮箱
            let subject = "测试邮件"
            let body = "这是一封来自 F# 应用程序的测试邮件。"
            try
                sendEmail from recipient subject body |> ignore
                return! text "测试邮件已发送。" next ctx
            with
            | ex ->
                return! text (sprintf "发送测试邮件失败: %s" ex.Message) next ctx
        }
