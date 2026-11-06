namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// A batch offered for sale on the marketplace by a farmer tenant.
/// The listing references a specific FlockBatch.
/// Brokers see only Published listings (never draft/cancelled ones from other tenants).
/// QuantityAvailable is decremented atomically when a deal is accepted.
/// </summary>
public class MarketplaceListing : TenantEntity
{
    public Guid BatchId { get; set; }
    public FlockBatch Batch { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public int QuantityTotal { get; set; }               // Birds offered
    public int QuantityAvailable { get; set; }           // Remaining after reservations
    public decimal AskingPricePerKg { get; set; }
    public decimal? NegotiableFloorPricePerKg { get; set; }
    public string LocationCity { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public ICollection<DealProposal> Proposals { get; set; } = new List<DealProposal>();

    // Concurrency token — used to detect simultaneous reservations
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[]? RowVersion { get; set; }
}
