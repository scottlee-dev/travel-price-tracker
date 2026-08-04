using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CancunScraper.Services;

public class EmailService
{
    public async Task SendEmailAsync(string subject, string body)
    {
        string senderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail") ?? "";
        string senderPassword = Environment.GetEnvironmentVariable("EmailSettings__SenderPassword") ?? "";
        string senderName = Environment.GetEnvironmentVariable("EmailSettings__SenderName") ?? "Cancun Price Tracker";
        string recipientEmail = Environment.GetEnvironmentVariable("EmailSettings__RecipientEmail") ?? senderEmail;

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
        {
            Console.WriteLine("[EmailService] Email settings or credentials are missing. Skipping email dispatch.");
            return;
        }

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
}