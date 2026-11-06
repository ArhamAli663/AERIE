namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Inventory record tracking feed stock balance per farm and category.
/// Tracks current stock, restocking thresholds, and optional recorded purchase prices.
/// </summary>
public class FeedInventory : TenantEntity
{
    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public string FeedCategory { get; set; } = "Starter"; // Pre-Starter, Starter, Grower, Finisher
    public decimal CurrentStockKg { get; set; }
    public decimal MinimumStockThresholdKg { get; set; } = 500m;
    public decimal? UnitPricePerKg { get; set; } // Actual recorded price if available; null if unrecorded
    public string? SupplierName { get; set; }
    public DateTime LastRestockedAt { get; set; } = DateTime.UtcNow;
}
