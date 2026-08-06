using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CancunScraper.Services;

public class EmailService
{
    public async Task SendEmailAsync(string subject, string body)
    {
        string senderEmail = GetEnv("EMAIL_SENDER_EMAIL", "EmailSettings__SenderEmail");
        string senderPassword = GetEnv("EMAIL_SENDER_PASSWORD", "EmailSettings__SenderPassword");
        string senderName = GetEnv("EMAIL_SENDER_NAME", "EmailSettings__SenderName", "Cancun Price Tracker");
        string recipientEmail = GetEnv("EMAIL_RECIPIENT_EMAIL", "EmailSettings__RecipientEmail", senderEmail);

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
        {
            Console.WriteLine("[EmailService] Email credentials missing. Skipping email dispatch.");
            return;
        }

        string smtpServer = "smtp.gmail.com";
        int smtpPort = 587;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(recipientEmail);

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

    private string GetEnv(string primaryKey, string secondaryKey, string defaultValue = "")
    {
        string? val = Environment.GetEnvironmentVariable(primaryKey);
        if (!string.IsNullOrWhiteSpace(val)) return val;

        val = Environment.GetEnvironmentVariable(secondaryKey);
        if (!string.IsNullOrWhiteSpace(val)) return val;

        return defaultValue;
    }
}