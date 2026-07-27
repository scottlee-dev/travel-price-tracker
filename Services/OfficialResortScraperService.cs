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
        // 1. Configure dates (Defaulting to the targeted March 2027 dates if not provided)
        DateTime arriveDate = checkIn ?? new DateTime(2027, 3, 22, 0, 0, 0, DateTimeKind.Utc);
        DateTime departDate = checkOut ?? new DateTime(2027, 3, 26, 0, 0, 0, DateTimeKind.Utc);

        string arriveStr = arriveDate.ToString("yyyy-MM-dd");
        string departStr = departDate.ToString("yyyy-MM-dd");
        
        // 2. Generate the direct booking URL (Deep Link) to bypass initial searches
        string directBookingUrl = $"https://be.synxis.com/?adult={adults}&arrive={arriveStr}&chain=10237&child={children}&currency=USD&depart={departStr}&hotel=56627&level=hotel&locale=en-US&productcurrency=USD&rooms=1";

        Console.WriteLine("[OfficialScraper] Initializing Playwright engine for Direct Booking Portal...");
        using var playwright = await Playwright.CreateAsync();

        // 3. Launch browser in Headless mode for silent background execution
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
            
            // Wait ONLY until the target room name appears on the screen (ensures the SPA has loaded the data)
            var roomTitleLocator = page.GetByText(targetRoomName).First;
            await roomTitleLocator.WaitForAsync(new LocatorWaitForOptions 
            { 
                State = WaitForSelectorState.Visible, 
                Timeout = 30000 
            });

            Console.WriteLine($"[OfficialScraper] Successfully detected target room: '{targetRoomName}'!");

            // -----------------------------------------------------------------------------------------
            // STEP 2 & 3: TEXT-FIRST EXTRACTION & REGEX PARSING (Bypassing Brittle DOM Traversal)
            // -----------------------------------------------------------------------------------------
            Console.WriteLine("[OfficialScraper] Bypassing complex DOM traversal. Extracting raw page text...");

            // Extract the raw, human-readable text of the entire page body instantly
            string fullPageText = await page.Locator("body").InnerTextAsync();

            // Find the exact starting position of our target room to isolate it
            int roomStartIndex = fullPageText.IndexOf(targetRoomName, StringComparison.OrdinalIgnoreCase);
            if (roomStartIndex == -1)
            {
                throw new Exception($"Could not find room '{targetRoomName}' in the extracted page text.");
            }

            // Slice the text from the target room onwards. 
            // This completely eliminates the risk of scraping prices from more expensive rooms listed above it.
            string textFromTargetRoom = fullPageText.Substring(roomStartIndex);

            Console.WriteLine($"[OfficialScraper] Parsing raw text for rate plan: '{targetRatePlan}'...");

            // Use Regex to find the target rate plan, followed by any characters, and capture the very first dollar amount.
            // Pattern explanation: Match rate plan -> [\s\S]*? (lazy match any characters/newlines) -> \$ -> Capture digits
            string escapedRatePlan = Regex.Escape(targetRatePlan);
            string pattern = $@"{escapedRatePlan}[\s\S]*?\$([0-9,]+(\.[0-9]{2})?)";

            var match = Regex.Match(textFromTargetRoom, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Fallback debug info if the layout changes drastically
                Console.WriteLine($"[Debug Text Snippet]\n{textFromTargetRoom.Substring(0, Math.Min(500, textFromTargetRoom.Length))}");
                throw new Exception($"Failed to match rate plan '{targetRatePlan}' and price pattern.");
            }

            // Clean and parse the extracted price pattern into a precise decimal
            string rawPriceMatch = match.Groups[1].Value;
            string cleanPriceString = rawPriceMatch.Replace(",", "");
            
            if (!decimal.TryParse(cleanPriceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal scrapedPrice))
            {
                throw new Exception($"Could not parse numeric string '{cleanPriceString}' into a decimal value.");
            }

            Console.WriteLine($"[OfficialScraper] SUCCESS! Regex successfully parsed real-time price: ${scrapedPrice:F2} USD");

            // Return the neatly packaged data object for the database and email alerts
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
            
            // Keep browser open briefly on failure if we ever switch Headless back to false for debugging
            await page.WaitForTimeoutAsync(5000);
            return null;
        }
        finally
        {
            // Ensure the browser instances are always cleanly closed to prevent memory leaks
            await browser.CloseAsync();
        }
    }
}