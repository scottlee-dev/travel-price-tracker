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
            await page.WaitForTimeoutAsync(10000); // Allow dynamic DOM elements to settle

            // 1. Scope to the specific Room Card that contains targetRoomName
            var roomCard = page.Locator("div.thumb-cards_card, div.thumb-cards_item, div.thumb-cards_info-container, div[class*='thumb-cards']")
                .Filter(new() { HasText = targetRoomName });

            if (await roomCard.CountAsync() == 0)
            {
                _logger.LogWarning("[Scraper] Target room block not found: {RoomName}", targetRoomName);
                return 0m;
            }

            // 2. Scope to the target Rate Plan INSIDE that specific room card
            var ratePlanBlock = roomCard.First.Locator("div.thumb-cards_rate")
                .Filter(new() { HasText = targetRatePlan });

            if (await ratePlanBlock.CountAsync() == 0)
            {
                _logger.LogWarning("[Scraper] Rate plan '{RatePlan}' not found inside room '{RoomName}'", targetRatePlan, targetRoomName);
                return 0m;
            }

            // 3. Extract price text from selector
            string priceText = await ratePlanBlock.First.Locator(".thumb-cards_price").First.InnerTextAsync();

            if (decimal.TryParse(priceText.Replace("$", "").Replace(",", "").Trim(), out var price))
            {
                _logger.LogInformation("[Scraper] Successfully parsed price for {RoomName}: ${Price}", targetRoomName, price);
                return price;
            }

            // 4. Scoped Regex Fallback (Search only inside the rate plan block, not entire body)
            _logger.LogWarning("[Scraper] Direct selector parse failed. Attempting Scoped Regex fallback...");
            string blockText = await ratePlanBlock.First.InnerTextAsync();
            price = TryParsePriceFromText(blockText);

            if (price > 0m)
            {
                _logger.LogInformation("[Scraper] Successfully parsed price via Scoped Regex fallback: ${Price}", price);
                return price;
            }

            _logger.LogWarning("[Scraper] Failed to parse price for target room.");
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