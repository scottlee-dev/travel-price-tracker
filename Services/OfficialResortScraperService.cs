using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CancunScraper.Services;

public class OfficialResortScraperService
{
    private readonly ILogger<OfficialResortScraperService> _logger;

    public OfficialResortScraperService(ILogger<OfficialResortScraperService> logger)
    {
        _logger = logger;
    }

    public async Task<decimal> ScrapeOfficialWebsiteAsync(
        string resortName, DateTime checkIn, DateTime checkOut,
        string targetRoomName, string targetRatePlan, int adults, int children)
    {
        _logger.LogInformation("[OfficialScraper] Initializing Playwright engine...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
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

        _logger.LogInformation("[OfficialScraper] Navigating: {Url}", url);

        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });

        // Wait for rate plans to load
        await page.WaitForSelectorAsync(".rate-plan", new() { Timeout = 30000 });

        // Find the rate plan block
        var ratePlanBlock = page.Locator($"text={targetRatePlan}").Locator("..");

        if (await ratePlanBlock.CountAsync() == 0)
        {
            _logger.LogWarning("[OfficialScraper] Rate plan not found: {RatePlan}", targetRatePlan);
            return 0m;
        }

        // Extract price inside the rate plan block
        var priceText = await ratePlanBlock.Locator(".price").InnerTextAsync();

        if (decimal.TryParse(priceText.Replace("$", "").Replace(",", ""), out var price))
        {
            _logger.LogInformation("[OfficialScraper] SUCCESS! Parsed price: ${Price}", price);
            return price;
        }

        _logger.LogWarning("[OfficialScraper] Failed to parse price text: {Text}", priceText);
        return 0m;
    }
}
