using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CancunScraper.Services;

public class EmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(string subject, string body)
    {
        string senderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail");
        string senderPassword = Environment.GetEnvironmentVariable("EmailSettings__SenderPassword"); // Gmail 앱 비밀번호
        string senderName = Environment.GetEnvironmentVariable("EmailSettings__SenderName");
        string recipientEmail = Environment.GetEnvironmentVariable("EmailSettings__RecipientEmail");

        // Gmail SMTP 고정값
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

            _logger.LogInformation("[EmailService] Sending email to {Recipient}...", recipientEmail);
            await smtpClient.SendMailAsync(message);
            _logger.LogInformation("[EmailService] Email sent successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmailService] Failed to send email.");
        }
    }
}
