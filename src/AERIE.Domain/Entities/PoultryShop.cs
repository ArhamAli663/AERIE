namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Represents a Poultry Shop, Wholesale Mandi Commission Shop, or Retail Chicken Outlet.
/// Enables farmers and brokers to discover, search, analyze, and trade directly with retail shops.
/// </summary>
public class PoultryShop : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = "Punjab";
    public string MarketName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ShopType { get; set; } = "Wholesale Mandi Shop"; // Wholesale Mandi Shop, Retail Chicken Point, Corporate Farm Outlet, Processor
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public decimal DailyCapacityKg { get; set; } = 2500m;
    public decimal CurrentBroilerMeatRatePerKg { get; set; } = 550m;
    public decimal CurrentLiveBroilerBuyRatePerKg { get; set; } = 345m;
    public double GoogleRating { get; set; } = 4.6;
    public int GoogleReviewsCount { get; set; } = 52;
    public bool IsGoogleVerified { get; set; } = true;
    public string OperatingHours { get; set; } = "06:00 AM - 10:00 PM";
    public string Specialties { get; set; } = "Whole Broiler, Boneless, Live Bird Cutting, Daily Mandi Crates";
    public string? Notes { get; set; }
}
