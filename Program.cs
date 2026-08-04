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

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scraper");
logger.LogInformation("Starting Cancun price scraping job on GitHub Actions...");

// --- Environment Variables ---
string searchRoom = Environment.GetEnvironmentVariable("SEARCH_ROOM")
    ?? "Ocean Front Suite Double (2 Queen)";

string searchRate = Environment.GetEnvironmentVariable("SEARCH_RATE")
    ?? "I Prefer Member Rate";

decimal targetThreshold = decimal.TryParse(
    Environment.GetEnvironmentVariable("TARGET_THRESHOLD"), out var th)
    ? th : 955.00m;

DateTime checkIn = DateTime.TryParse(
    Environment.GetEnvironmentVariable("CHECK_IN"), out var ci)
    ? ci : new DateTime(2027, 3, 22);

DateTime checkOut = DateTime.TryParse(
    Environment.GetEnvironmentVariable("CHECK_OUT"), out var co)
    ? co : new DateTime(2027, 3, 26);

int routineInterval = int.TryParse(
    Environment.GetEnvironmentVariable("ROUTINE_INTERVAL_DAYS"), out var ri)
    ? ri : 3;

// --- Main Execution ---
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
                body: $"The price dropped below your target of ${targetThreshold}\n\n" +
                      $"Current Price: ${currentPrice}\nRoom: {searchRoom}\nRate Plan: {searchRate}\n\n" +
                      $"Book it now before it changes."
            );
        }

        if (DateTime.UtcNow.DayOfYear % routineInterval == 0)
        {
            logger.LogInformation("Sending routine status report email...");
            await emailService.SendEmailAsync(
                subject: $"[Status Report] Cancun Tracker Running (Current: ${currentPrice})",
                body: $"The price tracker executed.\n\n" +
                      $"- Current Price: ${currentPrice}\n" +
                      $"- Target Threshold: ${targetThreshold}\n" +
                      $"- Room: {searchRoom}\n" +
                      $"- Rate Plan: {searchRate}\n" +
                      $"- Checked At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                      $"Routine interval: {routineInterval} days."
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

// --- Helper Methods ---
static void SavePriceHistoryAndGenerateChart(decimal currentPrice)
{
    string historyFile = "price_history.csv";
    string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

    lock (typeof(Program))
    {
        if (!File.Exists(historyFile))
            File.WriteAllText(historyFile, "Date,Price\n");

        File.AppendAllText(historyFile, $"{todayStr},{currentPrice}\n");
    }

    var lines = File.ReadAllLines(historyFile)
        .Skip(1)
        .Where(l => !string.IsNullOrWhiteSpace(l));

    List<DateTime> dates = new();
    List<double> prices = new();

    foreach (var line in lines)
    {
        var parts = line.Split(',');
        if (parts.Length == 2 &&
            DateTime.TryParse(parts[0], out var d) &&
            double.TryParse(parts[1], out var p))
        {
            dates.Add(d);
            prices.Add(p);
        }
    }

    if (dates.Count == 0) return;

    var plt = new Plot();
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
