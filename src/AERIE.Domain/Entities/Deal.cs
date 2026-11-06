namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// An accepted, active trade deal between a farmer and broker.
/// Created atomically when a farmer accepts a DealProposal.
/// Both farmer and broker reference this same deal record.
/// The deal progresses through explicit status transitions.
/// </summary>
public class Deal : BaseEntity    // NOT TenantEntity — shared between two tenants
{
    public Guid ProposalId { get; set; }
    public DealProposal Proposal { get; set; } = null!;

    public Guid ListingId { get; set; }
    public MarketplaceListing Listing { get; set; } = null!;

    public Guid FarmerTenantId { get; set; }
    public Guid BrokerTenantId { get; set; }

    public int QuantityBirds { get; set; }
    public decimal AgreedPricePerKg { get; set; }
    public decimal? EstimatedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }          // From weighbridge at lifting

    public DealStatus Status { get; set; } = DealStatus.Accepted;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public ICollection<DealStatusHistory> StatusHistory { get; set; } = new List<DealStatusHistory>();
    public ICollection<TransportRecord> TransportRecords { get; set; } = new List<TransportRecord>();
    public Settlement? Settlement { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
