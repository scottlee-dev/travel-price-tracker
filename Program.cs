using CancunScraper;
using CancunScraper.Data;
using CancunScraper.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");



builder.Services.AddDbContext<TravelDbContext>(option => option.UseNpgsql(connectionString));


builder.Services.AddTransient<CostcoScraperService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
