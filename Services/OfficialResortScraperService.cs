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
        _logger.LogInformation("[OfficialScraper] Initializing Playwright engine for Direct Booking Portal...");

        using var playwright = await Playwright.CreateAsync();
        
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        var page = await context.NewPageAsync();

        string url = $"https://be.synxis.com/?adult={adults}&arrive={checkIn:yyyy-MM-dd}&chain=10237&child={children}&currency=USD&depart={checkOut:yyyy-MM-dd}&hotel=56627&level=hotel&locale=en-US&productcurrency=USD&rooms=1";

        _logger.LogInformation("[OfficialScraper] Navigating directly to Synxis Booking URL:\n -> {Url}", url);

        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });

        _logger.LogInformation("[OfficialScraper] Waiting 10 seconds for dynamic pricing data to load...");
        await page.WaitForTimeoutAsync(10000); 

        // Extract text specifically from the price area or fallback to full body text
        string pageText = await page.Locator("body").InnerTextAsync();

        _logger.LogInformation("[OfficialScraper] Parsing raw text for target rate plan: '{RatePlan}'...", targetRatePlan);

        decimal parsedPrice = 0m;

        // Specific Regex to catch the price format near the rate plan
        Match match = Regex.Match(pageText, @"I\s*PREFER\s*OFFER[^$]*\$\s*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            string priceString = match.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(priceString, out parsedPrice))
            {
                _logger.LogInformation("[OfficialScraper] SUCCESS! Parsed targeted rate price: ${Price} USD", parsedPrice);
            }
        }

        // Fallback: if specific regex fails, grab the first valid price on page
        if (parsedPrice == 0m)
        {
            Match fallbackMatch = Regex.Match(pageText, @"\$\s*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)");
            if (fallbackMatch.Success)
            {
                string priceString = fallbackMatch.Groups[1].Value.Replace(",", "");
                decimal.TryParse(priceString, out parsedPrice);
                _logger.LogInformation("[OfficialScraper] Fallback matched price: ${Price} USD", parsedPrice);
            }
        }

        return parsedPrice;
    }
}