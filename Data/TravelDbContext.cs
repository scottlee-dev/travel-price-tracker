using CancunScraper.Models;
using Microsoft.EntityFrameworkCore;

namespace CancunScraper.Data;

public class TravelDbContext : DbContext
{
    public TravelDbContext(DbContextOptions<TravelDbContext> options) : base(options)
    {
    }

    public DbSet<HotelPriceLog> HotelPrices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<HotelPriceLog>().Property(p=> p.Price).HasPrecision(18,2);

     
    }


}
