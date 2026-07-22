using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CancunScraper.Data;
using CancunScraper.Services;

namespace CancunScraper;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Official Resort Price Tracker Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateAsyncScope();

                // Instantiate our new Official Website Scraper
                var officialScraper = new OfficialResortScraperService();
                var dbContext = scope.ServiceProvider.GetRequiredService<TravelDbContext>();

                string targetResort = "Grand Fiesta Americana Coral Beach Cancun Resort";
                
                // Set travel dates: March 22, 2027 to March 26, 2027
                DateTime checkInDate = new DateTime(2027, 3, 22, 0, 0, 0, DateTimeKind.Utc);
                DateTime checkOutDate = new DateTime(2027, 3, 26, 0, 0, 0, DateTimeKind.Utc);

                _logger.LogInformation("Starting Direct Portal scraper for: {Resort} ({CheckIn:yyyy-MM-dd} to {CheckOut:yyyy-MM-dd})",
                    targetResort, checkInDate, checkOutDate);
                
                // Call the direct Synxis booking portal scraper!
                var priceLog = await officialScraper.ScrapeOfficialWebsiteAsync(
                    resortName: targetResort,
                    checkIn: checkInDate,
                    checkOut: checkOutDate,
                    targetRoomName: "Ocean Front Suite Double (2 Queen)",
                    targetRatePlan: "I Prefer Member Rate",
                    adults: 3,
                    children: 0);

                if (priceLog != null)
                {
                    dbContext.HotelPrices.Add(priceLog);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Successfully saved official price to DB! ID: {Id}, Resort: {Resort}, Price: ${Price}, Source: {Source}",
                        priceLog.Id, priceLog.HotelName, priceLog.Price, priceLog.Source);
                }
                else
                {
                    _logger.LogWarning("Scraping returned null. No data saved to the database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running the official scraping pipeline.");


            }

            _logger.LogInformation("Sleeping for 30 seconds before the next check...\n-------------------------------------------------------------");
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}