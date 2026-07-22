using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using CancunScraper.Models;

namespace CancunScraper.Services;

public class OfficialResortScraperService
{
    public async Task<HotelPriceLog?> ScrapeOfficialWebsiteAsync(
        string resortName = "Grand Fiesta Americana Coral Beach Cancun Resort", 
        DateTime? checkIn = null, 
        DateTime? checkOut = null, 
        string targetRoomName = "Ocean Front Suite Double (2 Queen)", 
        string targetRatePlan = "I Prefer Member Rate",
        int adults = 3,
        int children = 0)
    {
        DateTime arriveDate = checkIn ?? new DateTime(2027, 3, 22, 0, 0, 0, DateTimeKind.Utc);
        DateTime departDate = checkOut ?? new DateTime(2027, 3, 26, 0, 0, 0, DateTimeKind.Utc);

        string arriveStr = arriveDate.ToString("yyyy-MM-dd");
        string departStr = departDate.ToString("yyyy-MM-dd");
        
        string directBookingUrl = $"https://be.synxis.com/?adult={adults}&arrive={arriveStr}&chain=10237&child={children}&currency=USD&depart={departStr}&hotel=56627&level=hotel&locale=en-US&productcurrency=USD&rooms=1";

        Console.WriteLine("[OfficialScraper] Initializing Playwright engine for Direct Booking Portal...");
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,  
            SlowMo = 0       
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });

        var page = await context.NewPageAsync();

        try
        {
            Console.WriteLine($"[OfficialScraper] Navigating directly to Synxis Booking URL:\n -> {directBookingUrl}");
            await page.GotoAsync(directBookingUrl);

            // -----------------------------------------------------------------------------------------
            // STEP 1: WAIT FOR SYNXIS SPA TO RENDER ROOM LISTINGS IN THE DOM
            // -----------------------------------------------------------------------------------------
            Console.WriteLine("[OfficialScraper] Waiting for Synxis JavaScript engine to render room cards...");
            
            var roomTitleLocator = page.GetByText(targetRoomName).First;
            await roomTitleLocator.WaitForAsync(new LocatorWaitForOptions 
            { 
                State = WaitForSelectorState.Visible, 
                Timeout = 30000 
            });

            Console.WriteLine($"[OfficialScraper] Successfully detected target room: '{targetRoomName}'!");

            // -----------------------------------------------------------------------------------------
            // STEP 2: ISOLATE THE SPECIFIC ROOM CARD & RATE PLAN ROW
            // -----------------------------------------------------------------------------------------
            Console.WriteLine($"[OfficialScraper] Isolating DOM container for room: '{targetRoomName}'...");
            
            // Locate the innermost room container box that holds the room title and "Room Details"
            var roomCard = page.Locator("div, section, article")
                               .Filter(new() { HasText = targetRoomName })
                               .Filter(new() { HasText = "Room Details" })
                               .First;

            Console.WriteLine($"[OfficialScraper] Drilling down into rate plan: '{targetRatePlan}'...");
            
            // Inside the isolated room card, locate the specific row containing our rate plan and a dollar sign
            var ratePlanRow = roomCard.Locator("div, tr, li")
                                      .Filter(new() { HasText = targetRatePlan })
                                      .Filter(new() { HasText = "$" })
                                      .Last;

            // -----------------------------------------------------------------------------------------
            // STEP 3: EXTRACT & PARSE THE REAL-TIME PRICE VIA REGULAR EXPRESSIONS
            // -----------------------------------------------------------------------------------------
            string rawRateText = await ratePlanRow.InnerTextAsync();
            Console.WriteLine($"[OfficialScraper] Raw DOM text extracted from rate row:\n-------------------------------------------------------------\n{rawRateText}\n-------------------------------------------------------------");

            // Use pattern matching to extract numerical value following the '$' symbol (e.g., "$1,014" -> "1,014")
            var match = Regex.Match(rawRateText, @"\$\s*([0-9,]+(\.[0-9]{2})?)");
            
            if (!match.Success)
            {
                throw new Exception($"Failed to extract dollar price pattern from text: {rawRateText}");
            }

            string cleanPriceString = match.Groups[1].Value.Replace(",", "");
            
            if (!decimal.TryParse(cleanPriceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal scrapedPrice))
            {
                throw new Exception($"Could not parse numeric string '{cleanPriceString}' into a decimal value.");
            }

            Console.WriteLine($"[OfficialScraper] SUCCESS! Extracted real-time price: ${scrapedPrice:F2} USD");

            return new HotelPriceLog
            {
                HotelName = resortName,
                Price = scrapedPrice,
                CheckInDate = DateTime.SpecifyKind(arriveDate, DateTimeKind.Utc),
                CheckOutDate = DateTime.SpecifyKind(departDate, DateTimeKind.Utc),
                Source = "Official Website (Synxis)"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n====================================================================");
            Console.WriteLine($"[OfficialScraper Error] Pipeline failed: {ex.Message}");
            Console.WriteLine("====================================================================\n");
            
            Console.WriteLine("[OfficialScraper Debug] Keeping browser open for 10 seconds for visual debugging before closing...");
            await page.WaitForTimeoutAsync(10000);
            return null;
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}