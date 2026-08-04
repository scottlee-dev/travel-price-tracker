using System;

namespace CancunScraper.Models;

public class HotelPriceLog
{
    public long Id { get; set; }

    public required string HotelName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    public required string Source { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
