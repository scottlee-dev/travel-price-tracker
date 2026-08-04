using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        _logger.LogInformation("[Scraper] Initializing Playwright browser instance...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            SlowMo = 100
        });

        var context = await browser.NewContextAsync(new()
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new() { Width = 1920, Height = 1080 },
            BypassCSP = true,
            JavaScriptEnabled = true
        });

        var page = await context.NewPageAsync();

        string url = $"https://be.synxis.com/?adult={adults}&arrive={checkIn:yyyy-MM-dd}&chain=10237&child={children}" +
                    $"&currency=USD&depart={checkOut:yyyy-MM-dd}&hotel=56627&level=hotel&locale=en-US&productcurrency=USD&rooms=1";

        _logger.LogInformation("[Scraper] Navigating to booking page: {Url}", url);

        try
        {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
            await page.WaitForTimeoutAsync(10000);

            var roomTitle = page.Locator("h2.app_heading1")
                .Filter(new() { HasText = targetRoomName });

            if (await roomTitle.CountAsync() == 0)
            {
                _logger.LogWarning("[Scraper] Target room title element not found: {RoomName}", targetRoomName);
                return 0m;
            }

            var targetRoomCard = roomTitle.First.Locator("xpath=ancestor::div[contains(@class, 'thumb-cards_show') or contains(@class, 'thumb-cards_card')][1]");

            if (await targetRoomCard.CountAsync() == 0)
            {
                _logger.LogWarning("[Scraper] Could not find parent room card container for: {RoomName}", targetRoomName);
                return 0m;
            }

            var targetRateBlock = targetRoomCard.Locator("div.thumb-cards_rate")
                .Filter(new() { HasText = targetRatePlan });

            if (await targetRateBlock.CountAsync() == 0)
            {
                _logger.LogWarning("[Scraper] Rate plan '{RatePlan}' not found under room '{RoomName}'", targetRatePlan, targetRoomName);
                return 0m;
            }

            var priceLocator = targetRateBlock.First.Locator("[data-testid='regular-price'], .thumb-cards_price").First;
            string priceText = await priceLocator.InnerTextAsync();

            if (decimal.TryParse(priceText.Replace("$", "").Replace(",", "").Trim(), out var price))
            {
                _logger.LogInformation("[Scraper] Successfully extracted exact price for {RoomName}: ${Price}", targetRoomName, price);
                return price;
            }

            _logger.LogWarning("[Scraper] Selector parsing failed. Attempting scoped regex fallback...");
            string rateBlockText = await targetRateBlock.First.InnerTextAsync();
            price = TryParsePriceFromText(rateBlockText);

            if (price > 0m)
            {
                _logger.LogInformation("[Scraper] Successfully parsed price via Scoped Regex: ${Price}", price);
                return price;
            }

            _logger.LogWarning("[Scraper] Failed to parse price for {RoomName}.", targetRoomName);
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scraper] An exception occurred during scraping execution.");
            return 0m;
        }
    }

    private decimal TryParsePriceFromText(string text)
    {
        var match = Regex.Match(text, @"\$\s*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out var price))
        {
            return price;
        }

        return 0m;
    }
}