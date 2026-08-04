using System;
using System.IO;
using CancunScraper.Services;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<OfficialResortScraperService>();

// Scraper configuration
string resortName = "Grand Fiesta Americana Coral Beach Cancun Resort";
string targetRoomName = Environment.GetEnvironmentVariable("SEARCH_ROOM") 
    ?? "Ocean Front Suite Double (2 Queen)";
string targetRatePlan = Environment.GetEnvironmentVariable("SEARCH_RATE") 
    ?? "I Prefer Member Rate";

decimal targetPrice = decimal.TryParse(
    Environment.GetEnvironmentVariable("TARGET_THRESHOLD"), out var th)
    ? th : 955m;

// Dates (static or environment)
DateTime checkIn = DateTime.TryParse(
    Environment.GetEnvironmentVariable("CHECK_IN"), out var ci)
    ? ci : new DateTime(2027, 3, 22);

DateTime checkOut = DateTime.TryParse(
    Environment.GetEnvironmentVariable("CHECK_OUT"), out var co)
    ? co : new DateTime(2027, 3, 26);

// Initialize scraper
var scraper = new OfficialResortScraperService(logger);

logger.LogInformation("Starting Cancun price scraping job...");

decimal price = await scraper.ScrapeOfficialWebsiteAsync(
    resortName,
    checkIn,
    checkOut,
    targetRoomName,
    targetRatePlan,
    adults: 3,
    children: 0
);

// If scrape failed
if (price <= 0)
{
    logger.LogWarning("Scraper returned no price. Exiting.");
    return;
}

// Email alert if below target
if (price <= targetPrice)
{
    var emailService = new EmailService();
    await emailService.SendEmailAsync(
        subject: $"[PRICE DROP ALERT] Cancun Resort Deal - ${price}",
        body: $"The price dropped below your target of ${targetPrice}\n\n" +
              $"Current Price: ${price}\nRoom: {targetRoomName}\nRate Plan: {targetRatePlan}\n\n" +
              $"Book it now before it changes."
    );
}

// Update README dashboard
UpdateReadme(price, targetPrice, targetRoomName, targetRatePlan, checkIn, checkOut);

logger.LogInformation("Scraping job finished successfully.");


// ------------------ Helper: Update README ------------------

void UpdateReadme(decimal currentPrice, decimal targetPrice, string room, string rate, DateTime inDate, DateTime outDate)
{
    string readmePath = "README.md";

    string dashboardContent = $@"<!-- START_DASHBOARD -->
## Price Trend Graph

![Price Trend](price_trend.png)

## Live Dashboard

| Attribute | Details |
|---|---|
| **Resort** | Grand Fiesta Americana Coral Beach Cancun |
| **Room Type** | `{room}` |
| **Rate Plan** | `{rate}` |
| **Dates** | {inDate:yyyy-MM-dd} ~ {outDate:yyyy-MM-dd} |
| **Current Price** | **${currentPrice}** |
| **Target Threshold** | **${targetPrice}** |
| **Status** | {(currentPrice <= targetPrice ? "**DEAL DETECTED**" : "**Monitoring**")} |
| **Last Updated** | `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC` |
<!-- END_DASHBOARD -->";

    string content = File.Exists(readmePath)
        ? File.ReadAllText(readmePath)
        : "";

    int start = content.IndexOf("<!-- START_DASHBOARD -->");
    int end = content.IndexOf("<!-- END_DASHBOARD -->");

    if (start >= 0 && end > start)
    {
        string before = content[..start];
        string after = content[(end + "<!-- END_DASHBOARD -->".Length)..];
        File.WriteAllText(readmePath, before + dashboardContent + after);
    }
    else
    {
        File.WriteAllText(readmePath,
            "# Cancun Resort Price Tracker\n\n" +
            "Automated price tracking pipeline built with **C# .NET**, **Playwright**, and **GitHub Actions**.\n\n" +
            dashboardContent);
    }
}
