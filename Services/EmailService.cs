using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CancunScraper.Services;

public class EmailService
{
    public async Task SendEmailAsync(string subject, string body)
    {
        string senderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail");
        string senderPassword = Environment.GetEnvironmentVariable("EmailSettings__SenderPassword");
        string senderName = Environment.GetEnvironmentVariable("EmailSettings__SenderName");
        string recipientEmail = Environment.GetEnvironmentVariable("EmailSettings__RecipientEmail");

        string smtpServer = "smtp.gmail.com";
        int smtpPort = 587;

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(recipientEmail);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;

            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            await smtpClient.SendMailAsync(message);
            Console.WriteLine($"[EmailService] Email sent successfully to {recipientEmail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService] Failed to send email: {ex.Message}");
        }
    }

    public void SendAlert(decimal price)
    {
        string subject = $"[PRICE DROP ALERT] Cancun Resort Deal - ${price}";
        string body = $"The price dropped below your target of ${TargetPrice}\n\nCurrent Price: ${price}\nBook it now before it changes.";
        SendEmailAsync(subject, body).Wait();
    }

    private const decimal TargetPrice = 950m;
}
