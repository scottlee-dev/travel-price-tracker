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

                var officialScraper = scope.ServiceProvider.GetRequiredService<OfficialResortScraperService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<TravelDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>(); // Inject Email Service

                string targetResort = "Grand Fiesta Americana Coral Beach Cancun Resort";
                DateTime checkInDate = new DateTime(2027, 3, 22, 0, 0, 0, DateTimeKind.Utc);
                DateTime checkOutDate = new DateTime(2027, 3, 26, 0, 0, 0, DateTimeKind.Utc);

                _logger.LogInformation("Starting Direct Portal scraper for: {Resort}...", targetResort);
                
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

                    _logger.LogInformation("Successfully saved official price to DB! Price: ${Price}", priceLog.Price);

    
                    decimal targetPriceThreshold = 2100.00m;

                    if (priceLog.Price <= targetPriceThreshold)
                    {
                        _logger.LogInformation("Price (${Price}) is below threshold (${Threshold})! Triggering Email Alert...", priceLog.Price, targetPriceThreshold);
                        await emailService.SendPriceAlertAsync(targetResort, priceLog.Price, checkInDate.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        _logger.LogInformation("Price (${Price}) is still above threshold (${Threshold}). No email sent.", priceLog.Price, targetPriceThreshold);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running the official scraping pipeline.");
            }

            _logger.LogInformation("Sleeping for 4 hours before the next check...\n-------------------------------------------------------------");
            
            // Set delay to 4 hours for production
            await Task.Delay(TimeSpan.FromHours(4), stoppingToken); 
        }
    }
}