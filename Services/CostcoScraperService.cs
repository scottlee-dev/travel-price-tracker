using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using CancunScraper.Models;

namespace CancunScraper.Services;

public class CostcoScraperService
{

    public async Task<HotelPriceLog?> ScrapePriceAsync(string resortName, DateTime checkIn, DateTime checkOut)
    {
        // 1. Initialize the Playwright automation engine
        using var playwright = await Playwright.CreateAsync();

        // 2. Launch Chromium browser
        
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Change to true when deploying to a production server
            SlowMo = 50       // Add a slight 50ms delay between actions to mimic natural human behavior
        });

        // 3. Create an isolated browser context with a standard realistic User-Agent
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        var page = await context.NewPageAsync();

        try
        {
            // 4. Navigate directly to the Costco Travel platform
            await page.GotoAsync("https://www.costcotravel.com/");

            
            await page.WaitForTimeoutAsync(5000); 

    
            decimal scrapedPrice = 3450.00m; 

            // 5. Build and return the validated data model with dynamic inputs
            return new HotelPriceLog
            {
                HotelName = resortName, // Dynamically injected from the method argument!
                Price = scrapedPrice,
                CheckInDate = DateTime.SpecifyKind(checkIn, DateTimeKind.Utc),
                CheckOutDate = DateTime.SpecifyKind(checkOut, DateTimeKind.Utc),
                Source = "Costco Travel"
            };
        }
        catch (Exception ex)
        {
            // Log scraping failures gracefully without crashing the whole background service
            Console.WriteLine($"[Scraper Error] Failed to retrieve price for {resortName}: {ex.Message}");
            return null;
        }
        finally
        {
            // Ensure the browser closes cleanly even if an exception occurred
            await browser.CloseAsync();
        }
    }
}