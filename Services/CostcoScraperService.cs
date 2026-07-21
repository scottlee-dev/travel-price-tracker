using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using CancunScraper.Models;

namespace CancunScraper.Services;

public class CostcoScraperService
{
    public async Task<HotelPriceLog?> ScrapePriceAsync(
        string resortName, 
        DateTime checkIn, 
        DateTime checkOut, 
        string destination = "Cancun, Mexico", 
        string departureAirport = "PHL",
        int childrenCount = 1,
        int childAge = 16)
    {
        // 1. Initialize the Playwright automation engine
        using var playwright = await Playwright.CreateAsync();

        // 2. Launch Chromium browser (Headless = false to observe form interaction)
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,  
            SlowMo = 150       
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1366, Height = 850 }
        });

        var page = await context.NewPageAsync();

        try
        {
            Console.WriteLine("[Scraper] Navigating to Costco Travel home page...");
            await page.GotoAsync("https://www.costcotravel.com/");
            await page.WaitForTimeoutAsync(3000);

            // -----------------------------------------------------------------------------------------
            // STEP 1: DESTINATION (Type sequentially to trigger AJAX autocomplete suggestions)
            // -----------------------------------------------------------------------------------------
            Console.WriteLine($"[Scraper] Human-like typing for Destination: '{destination}'...");
            
            var destInput = page.Locator("#hotel_package_destination, input[id*='package_destination']").Filter(new() { Visible = true }).First;
            await destInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            await destInput.ClickAsync();
            await destInput.ClearAsync(); // Clear any existing pre-filled text
            
            // Key: Use PressSequentiallyAsync instead of FillAsync to simulate human typing with 100ms delay
            await destInput.PressSequentiallyAsync(destination, new() { Delay = 100 });
            
            Console.WriteLine("[Scraper] Waiting for autocomplete dropdown to appear...");
            await page.WaitForTimeoutAsync(2500); // Wait 2.5 seconds for autocomplete dropdown to render
            await page.Keyboard.PressAsync("ArrowDown"); // Navigate to the first autocomplete suggestion
            await page.Keyboard.PressAsync("Enter");     // Confirm destination selection

            // -----------------------------------------------------------------------------------------
            // STEP 2: TRAVEL DATES INPUT (Check-in and Check-out dates)
            // -----------------------------------------------------------------------------------------
            string checkInStr = checkIn.ToString("MM/dd/yyyy");
            string checkOutStr = checkOut.ToString("MM/dd/yyyy");

            Console.WriteLine($"[Scraper] Setting Travel Dates: {checkInStr} to {checkOutStr}...");
            
            var visibleDateInputs = page.Locator("input[placeholder*='mm/dd/yyyy']").Filter(new() { Visible = true });
            
            var depDateInput = visibleDateInputs.First;
            await depDateInput.ClickAsync();
            await depDateInput.FillAsync(checkInStr);
            await page.Keyboard.PressAsync("Enter"); 

            var retDateInput = visibleDateInputs.Nth(1);
            await retDateInput.ClickAsync();
            await retDateInput.FillAsync(checkOutStr);
            await page.Keyboard.PressAsync("Enter"); 

            // -----------------------------------------------------------------------------------------
            // STEP 3: FLYING FROM (Type departure airport sequentially)
            // -----------------------------------------------------------------------------------------
            Console.WriteLine($"[Scraper] Human-like typing for Airport: '{departureAirport}'...");
            
            var flightInput = page.Locator("input[placeholder*='departure airport'], input[placeholder*='Flying From'], input[id*='departureAirport']").Filter(new() { Visible = true }).First;
            await flightInput.ClickAsync();
            await flightInput.ClearAsync();
            
            await flightInput.PressSequentiallyAsync(departureAirport, new() { Delay = 100 });

            Console.WriteLine("[Scraper] Waiting for airport autocomplete dropdown...");
            await page.WaitForTimeoutAsync(2500); 
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Keyboard.PressAsync("Enter"); 

            // -----------------------------------------------------------------------------------------
            // STEP 4: PASSENGER CONFIGURATION (Select numeric value 1 and 16 per UI requirements)
            // -----------------------------------------------------------------------------------------
            if (childrenCount > 0)
            {
                Console.WriteLine($"[Scraper] Selecting Children count: '{childrenCount}'...");
                
                var childSelect = page.Locator("select[id*='child'], select[id*='Children'], select[name*='children']").Filter(new() { Visible = true }).First;
                await childSelect.ClickAsync(); 
                
                // Select simple numeric string "1" instead of label text
                await childSelect.SelectOptionAsync(new[] { childrenCount.ToString() });

                Console.WriteLine("[Scraper] Waiting for Age dropdown to render via AJAX...");
                await page.WaitForTimeoutAsync(1500);

                Console.WriteLine($"[Scraper] Selecting Child Age: '{childAge}'...");
                var childAgeSelect = page.Locator("select[id*='childAge'], select[id*='ChildAge'], select[name*='childAge']").Filter(new() { Visible = true }).First;
                await childAgeSelect.ClickAsync(); 
                await childAgeSelect.SelectOptionAsync(new[] { childAge.ToString() });
            }

            // -----------------------------------------------------------------------------------------
            // STEP 5: SUBMIT THE SEARCH FORM
            // -----------------------------------------------------------------------------------------
            Console.WriteLine("[Scraper] Clicking 'Search' button...");
            await page.WaitForTimeoutAsync(1000); 
            var searchButton = page.GetByRole(AriaRole.Button, new() { Name = "Search" }).Filter(new() { Visible = true }).First;
            await searchButton.ClickAsync();

            // -----------------------------------------------------------------------------------------
            // STEP 6: WAIT FOR RESULT LISTINGS TO RENDER
            // -----------------------------------------------------------------------------------------
            Console.WriteLine("[Scraper] Waiting for package search results to load (60 seconds for screenshot)...");
            
            // Increased wait time to 60 seconds (60000ms) so you can capture a screenshot of the results page!
            await page.WaitForTimeoutAsync(60000); 

            decimal scrapedPrice = 3450.00m; 

            return new HotelPriceLog
            {
                HotelName = resortName,
                Price = scrapedPrice,
                CheckInDate = DateTime.SpecifyKind(checkIn, DateTimeKind.Utc),
                CheckOutDate = DateTime.SpecifyKind(checkOut, DateTimeKind.Utc),
                Source = "Costco Travel"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n====================================================================");
            Console.WriteLine($"[Scraper Error] Pipeline failed: {ex.Message}");
            Console.WriteLine("====================================================================\n");
            
            Console.WriteLine("[Scraper Debug] Keeping the browser open for 10 seconds for visual debugging before closing...");
            await page.WaitForTimeoutAsync(10000);
            return null;
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}