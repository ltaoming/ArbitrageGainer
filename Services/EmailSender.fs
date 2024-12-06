module EmailSender

open MimeKit
open MailKit.Net.Smtp
open MailKit.Security

let sendEmail (from: string) (recipient: string) (subject: string) (body: string) =
    let message = new MimeMessage()
    message.From.Add(new MailboxAddress("", from))
    message.To.Add(new MailboxAddress("", recipient))
    message.Subject <- subject
    
    let textPart = new TextPart("plain")
    textPart.Text <- body
    message.Body <- textPart

    use smtpClient = new SmtpClient()
    try
        smtpClient.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls)
        let email = "mlc2455213223@gmail.com" 
        let password = "icmbxychzxytfzze"  
        smtpClient.Authenticate(email, password)
        smtpClient.Send(message)
    finally
        smtpClient.Disconnect(true)