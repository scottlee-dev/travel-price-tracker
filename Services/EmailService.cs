using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CancunScraper.Services;


public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }
    public async Task SendPriceAlertAsync(string resortName, decimal currentPrice, string checkInDate)
    {
        var emailSettings = _config.GetSection("EmailSettings");
        string senderEmail = emailSettings["SenderEmail"];
        string senderPassword = emailSettings["SenderPassword"];
        string senderName = emailSettings["SenderName"];
        string recipientEmail = emailSettings["RecipientEmail"];

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail));


            message.Subject = $" [PRICE DROP ALERT] {resortName} is now ${currentPrice}!";
            message.Body = $"Great news!\n\n" +
                                       $"The price for {resortName} (Check-in: {checkInDate}) has dropped to our target!\n" +
                                       $"Current Price: ${currentPrice}\n\n" +
                                       $"Book now before it changes!";

            message.IsBodyHtml = false;


            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"], int.Parse(emailSettings["SmtpPort"]))
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            _logger.LogInformation("[EmailService] Sending price alert email to {Recipient}...", recipientEmail);
            await smtpClient.SendMailAsync(message);
            _logger.LogInformation("[EmailService] Email sent successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmailService] Failed to send email alert.");
        }
    }
}