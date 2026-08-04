using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CancunScraper.Services;
using ScottPlot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<OfficialResortScraperService>();
builder.Services.AddScoped<EmailService>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Cancun price scraping job on GitHub Actions...");

string searchRoom = Environment.GetEnvironmentVariable("SEARCH_ROOM")
    ?? "Ocean Front Suite Double (2 Queen)";

string searchRate = Environment.GetEnvironmentVariable("SEARCH_RATE")
    ?? "I Prefer Member Rate";

string thresholdEnv = Environment.GetEnvironmentVariable("TARGET_THRESHOLD")
    ?? "955.00";

decimal targetThreshold = decimal.Parse(thresholdEnv);

string checkInEnv = Environment.GetEnvironmentVariable("CHECK_IN")
    ?? "2027-03-22";

string checkOutEnv = Environment.GetEnvironmentVariable("CHECK_OUT")
    ?? "2027-03-26";

DateTime checkIn = DateTime.Parse(checkInEnv);
DateTime checkOut = DateTime.Parse(checkOutEnv);

int routineInterval = int.Parse(
    Environment.GetEnvironmentVariable("ROUTINE_INTERVAL_DAYS") ?? "3"
);

try
{
    using var scope = host.Services.CreateScope();
    var scraperService = scope.ServiceProvider.GetRequiredService<OfficialResortScraperService>();
    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

    decimal currentPrice = await scraperService.ScrapeOfficialWebsiteAsync(
        resortName: "Grand Fiesta Americana Coral Beach Cancun Resort",
        checkIn: checkIn,
        checkOut: checkOut,
        targetRoomName: searchRoom,
        targetRatePlan: searchRate,
        adults: 3,
        children: 0
    );

    if (currentPrice > 0)
    {
        if (currentPrice <= targetThreshold)
        {
            logger.LogInformation("PRICE DROP ALERT! Sending email...");
            await emailService.SendEmailAsync(
                subject: $"[PRICE DROP ALERT] Cancun Resort Deal - ${currentPrice}",
                body: $"The price has dropped below your target of ${targetThreshold}\n\nCurrent Price: ${currentPrice}\nRoom: {searchRoom}\nRate Plan: {searchRate}\n\nBook it now before it changes."
            );
        }

        if (DateTime.UtcNow.DayOfYear % routineInterval == 0)
        {
            logger.LogInformation("Sending routine status report email...");
            await emailService.SendEmailAsync(
                subject: $"[Status Report] Cancun Tracker Running (Current: ${currentPrice})",
                body: $"The price tracker executed.\n\n- Current Price: ${currentPrice}\n- Target Threshold: ${targetThreshold}\n- Room: {searchRoom}\n- Rate Plan: {searchRate}\n- Checked At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\nRoutine interval: {routineInterval} days."
            );
        }

        SavePriceHistoryAndGenerateChart(currentPrice);

        UpdateReadme(currentPrice, targetThreshold, searchRoom, searchRate, checkIn, checkOut);
    }

    logger.LogInformation("Scraping job finished successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred during the scraping job.");
    Environment.ExitCode = 1;
}



static void SavePriceHistoryAndGenerateChart(decimal currentPrice)
{
    string historyFile = "price_history.csv";
    string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

    if (!File.Exists(historyFile))
    {
        File.WriteAllText(historyFile, "Date,Price\n");
    }

    File.AppendAllText(historyFile, $"{todayStr},{currentPrice}\n");

    var lines = File.ReadAllLines(historyFile).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
    List<DateTime> dates = new();
    List<double> prices = new();

    foreach (var line in lines)
    {
        var parts = line.Split(',');
        if (parts.Length == 2 && DateTime.TryParse(parts[0], out var d) && double.TryParse(parts[1], out var p))
        {
            dates.Add(d);
            prices.Add(p);
        }
    }

    if (dates.Count == 0) return;

    var plt = new ScottPlot.Plot();
    double[] xs = dates.Select(d => d.ToOADate()).ToArray();
    double[] ys = prices.ToArray();

    var scatter = plt.Add.Scatter(xs, ys);
    scatter.LineWidth = 2.5f;
    scatter.Color = ScottPlot.Color.FromHex("#007ACC");

    plt.Axes.DateTimeTicksBottom();
    plt.Title("Grand Fiesta Americana Coral Beach Price Trend");
    plt.YLabel("Price ($)");

    plt.SavePng("price_trend.png", 800, 400);
}

static void UpdateReadme(decimal currentPrice, decimal targetPrice, string room, string rate, DateTime inDate, DateTime outDate)
{
    string readmePath = "README.md";
    bool isDeal = currentPrice <= targetPrice;
    string statusBadge = isDeal 
        ? " **DEAL DETECTED (Below Target!)**" 
        : " **Monitoring (Above Target)**";

    string dashboardContent = $@"<!-- START_DASHBOARD -->
##  Price Trend Graph

![Price Trend](price_trend.png)

##  Live Dashboard

| Attribute | Details |
|---|---|
| **Resort** | Grand Fiesta Americana Coral Beach Cancun |
| **Room Type** | `{room}` |
| **Rate Plan** | `{rate}` |
| **Dates** | {inDate:yyyy-MM-dd} ~ {outDate:yyyy-MM-dd} |
| **Current Price** | **${currentPrice}** |
| **Target Threshold** | **${targetPrice}** |
| **Status** | {statusBadge} |
| **Last Updated** | `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC` |
<!-- END_DASHBOARD -->";

    if (File.Exists(readmePath))
    {
        string existingContent = File.ReadAllText(readmePath);
        
        if (existingContent.Contains("<!-- START_DASHBOARD -->") && existingContent.Contains("<!-- END_DASHBOARD -->"))
        {
            int startIndex = existingContent.IndexOf("<!-- START_DASHBOARD -->");
            int endIndex = existingContent.IndexOf("<!-- END_DASHBOARD -->") + "<!-- END_DASHBOARD -->".Length;
            
            string newContent = existingContent.Remove(startIndex, endIndex - startIndex)
                                               .Insert(startIndex, dashboardContent);
            
            File.WriteAllText(readmePath, newContent);
            return;
        }
    }

    string fullTemplate = $@"#  Cancun Resort Price Tracker

Automated price tracking pipeline built with **C# .NET**, **Playwright**, and **GitHub Actions**.

## 📌 Project Overview
This project continuously monitors room rates for Grand Fiesta Americana Coral Beach Cancun Resort. 
When a drop below the target price threshold is detected, automated email notifications are dispatched.

{dashboardContent}

## 🏗️ Architecture
- **Language & Framework:** C# .NET 10 / Playwright
- **Automation:** GitHub Actions (Cron Scheduler)
- **Data Persistence:** PostgreSQL / CSV History
- **Visualization:** ScottPlot

---
*This repository is automatically updated by GitHub Actions when new price points are scraped.*
";

    File.WriteAllText(readmePath, fullTemplate);
}