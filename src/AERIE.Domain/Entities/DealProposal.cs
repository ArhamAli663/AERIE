namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// A broker's purchase proposal referencing a marketplace listing.
/// The farmer reviews and accepts or rejects proposals.
/// </summary>
public class DealProposal : TenantEntity   // TenantId = Broker's tenant
{
    public Guid ListingId { get; set; }
    public MarketplaceListing Listing { get; set; } = null!;

    public Guid BrokerTenantId { get; set; }            // Redundant for quick filter, same as TenantId
    public string BrokerCompanyName { get; set; } = string.Empty;
    public Guid ProposedByUserId { get; set; }

    public int QuantityRequested { get; set; }
    public decimal OfferedPricePerKg { get; set; }
    public string? TransportArrangement { get; set; }
    public string? Notes { get; set; }
    public DealStatus Status { get; set; } = DealStatus.Proposed;
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public string? FarmerResponse { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Navigation
    public Deal? Deal { get; set; }
}
