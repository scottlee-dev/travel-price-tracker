using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScottPlot;

namespace CancunScraper.Services;

public class OfficialResortScraperService
{
    private readonly ILogger<OfficialResortScraperService> _logger;
    private const string CsvPath = "price_history.csv";
    private const decimal TargetPrice = 950m;

    public OfficialResortScraperService(ILogger<OfficialResortScraperService> logger)
    {
        _logger = logger;
    }

    public async Task<decimal> ScrapeOfficialWebsiteAsync(
        string resortName, DateTime checkIn, DateTime checkOut,
        string targetRoomName, string targetRatePlan, int adults, int children)
    {
        _logger.LogInformation("[Scraper] Initializing Playwright...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            SlowMo = 250
        });
        var context = await browser.NewContextAsync(new()
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36",
            ViewportSize = new() { Width = 1920, Height = 1080 },
            BypassCSP = true,
            JavaScriptEnabled = true
        });

        var page = await context.NewPageAsync();

        string url =
            $"https://be.synxis.com/?adult={adults}&arrive={checkIn:yyyy-MM-dd}&chain=10237&child={children}" +
            $"&currency=USD&depart={checkOut:yyyy-MM-dd}&hotel=56627&level=hotel&locale=en-US&productcurrency=USD&rooms=1";

        _logger.LogInformation("[Scraper] Navigating: {Url}", url);

        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });

        await page.WaitForSelectorAsync(".rate-plan", new() { Timeout = 90000 });


        var ratePlanBlock = page.Locator($"text={targetRatePlan}").Locator("..");

        if (await ratePlanBlock.CountAsync() == 0)
        {
            _logger.LogWarning("[Scraper] Rate plan not found: {RatePlan}", targetRatePlan);
            return 0m;
        }

        var priceText = await ratePlanBlock.Locator(".price").InnerTextAsync();

        if (!decimal.TryParse(priceText.Replace("$", "").Replace(",", ""), out var price))
        {
            _logger.LogWarning("[Scraper] Failed to parse price: {Text}", priceText);
            return 0m;
        }

        _logger.LogInformation("[Scraper] SUCCESS! Parsed price: ${Price}", price);

        LogPrice(price);
        GenerateTrendGraph();
        CheckPriceAlert(price);

        return price;
    }

    private void LogPrice(decimal price)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-dd},{price}";
        File.AppendAllText(CsvPath, line + Environment.NewLine);
        _logger.LogInformation("[Scraper] Logged price to CSV.");
    }

    private void GenerateTrendGraph()
    {
        var lines = File.ReadAllLines(CsvPath)
            .TakeLast(10)
            .Select(l => l.Split(','))
            .Select(p => new { Date = DateTime.Parse(p[0]), Price = decimal.Parse(p[1]) })
            .ToList();

        var plt = new ScottPlot.Plot();
        plt.Add.Scatter(
            lines.Select(x => x.Date.ToOADate()).ToArray(),
            lines.Select(x => (double)x.Price).ToArray()
        );

        plt.Axes.DateTimeTicksBottom();
        plt.Title("10‑Day Price Trend");
        plt.SavePng("price_trend.png", 600, 400);
    }

    private void CheckPriceAlert(decimal price)
    {
        if (price < TargetPrice)
        {
            var emailService = new EmailService();
            emailService.SendAlert(price);
            _logger.LogInformation("[Scraper] ALERT SENT — Price below target!");
        }
    }
}
