using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CancunScraper.Services;
using Microsoft.Extensions.Logging;
using ScottPlot;

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
    ? th : 800m;

// Dates
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

// 1. Record history and generate chart image
SavePriceHistoryAndGenerateChart(price);

// 2. Email alert if below target
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

// 3. Update Readme
UpdateReadme(price, targetPrice, targetRoomName, targetRatePlan, checkIn, checkOut);

logger.LogInformation("Scraping job finished successfully.");


void SavePriceHistoryAndGenerateChart(decimal currentPrice)
{
    string historyFile = "price_history.csv";
    
    DateTime nowEastern;
    try
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        nowEastern = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }
    catch
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        nowEastern = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    string timeStr = nowEastern.ToString("yyyy-MM-dd HH:mm");

    if (!File.Exists(historyFile))
    {
        File.WriteAllText(historyFile, "Date,Price" + Environment.NewLine);
    }

    File.AppendAllText(historyFile, $"{timeStr},{currentPrice}" + Environment.NewLine);

    var lines = File.ReadAllLines(historyFile);
    if (lines.Length <= 1) return;

    List<DateTime> dates = new();
    List<double> prices = new();

    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        var parts = line.Split(',');
        if (parts.Length == 2 && DateTime.TryParse(parts[0], out var d) && double.TryParse(parts[1], out var p))
        {
            dates.Add(d);
            prices.Add(p);
        }
    }

    if (dates.Count == 0) return;

    var recentDates = dates.TakeLast(15).Select(d => d.ToOADate()).ToArray();
    var recentPrices = prices.TakeLast(15).ToArray();

    var plt = new ScottPlot.Plot();

    var scatter = plt.Add.Scatter(recentDates, recentPrices);
    scatter.LineWidth = 2.5f;
    scatter.MarkerSize = 5;
    scatter.Color = ScottPlot.Color.FromHex("#007ACC");

    plt.Axes.DateTimeTicksBottom();
    plt.Title("Grand Fiesta Americana Coral Beach Price Trend");
    plt.YLabel("Price ($)");

    plt.SavePng("price_trend.png", 800, 400);
}

void UpdateReadme(decimal currentPrice, decimal targetPrice, string room, string rate, DateTime inDate, DateTime outDate)
{
    string readmePath = "README.md";

    DateTime easternTime;
    try
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        easternTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }
    catch
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        easternTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    string imageUrl = $"price_trend.png?raw=true&v={timestamp}";

    string dashboardContent = $@"<!-- START_DASHBOARD -->
## Price Trend Graph

![Price Trend]({imageUrl})

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
| **Last Updated** | `{easternTime:yyyy-MM-dd HH:mm:ss} EDT` |
<!-- END_DASHBOARD -->";

    string content = File.Exists(readmePath) ? File.ReadAllText(readmePath) : "";

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