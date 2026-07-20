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
        _logger.LogInformation("Costco Travel Price Tracker Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateAsyncScope();

                var scraperService = scope.ServiceProvider.GetRequiredService<CostcoScraperService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<TravelDbContext>();

                string targetResort = "Grand Fiesta Americana Coral Beach";
                DateTime checkInDate = new DateTime(2027, 3, 15, 0, 0, 0, DateTimeKind.Utc);
                DateTime checkOutDate = new DateTime(2027, 3, 20, 0, 0, 0, DateTimeKind.Utc);

                _logger.LogInformation("Starting Playwright scraper for: {Resort} ({CheckIn:yyyy-MM-dd} to {CheckOut:yyyy-MM-dd})",
                                    targetResort, checkInDate, checkOutDate);
                var priceLog = await scraperService.ScrapePriceAsync(targetResort, checkInDate, checkOutDate);

                if (priceLog != null)
                {
                    // 4. Save the scraped pricing record into the PostgreSQL database
                    dbContext.HotelPrices.Add(priceLog);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Successfully saved price log to PostgreSQL! DB ID: {Id}, Resort: {Resort}, Price: ${Price}",
                        priceLog.Id, priceLog.HotelName, priceLog.Price);
                }
                else
                {
                    _logger.LogWarning("Scraping returned null. No data saved to the database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running the scraping pipeline.");
            }

            // 5. Wait before starting the next cycle (Set to 30 seconds for testing; change to hours for production!)
            _logger.LogInformation("Sleeping for 30 seconds before the next check...\n-------------------------------------------------------------");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

    }
}
