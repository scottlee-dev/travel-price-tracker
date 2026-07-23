using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CancunScraper.Data;
using CancunScraper.Services;

namespace CancunScraper;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddDbContext<TravelDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddScoped<OfficialResortScraperService>();
        builder.Services.AddTransient<EmailService>();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}