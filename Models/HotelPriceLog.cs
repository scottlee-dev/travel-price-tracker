
using System;

namespace CancunScraper.Models;

public class HotelPriceLog
{
    public long Id { get; set; }


    public required string HotelName { get; set; }

    public decimal Price { get; set; }

    public DateTime ChecInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    public required string Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}