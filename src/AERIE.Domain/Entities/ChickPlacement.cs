namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Records the initial chick placement event for a batch.
/// A batch may have one primary placement and possible supplemental placements.
/// </summary>
public class ChickPlacement : TenantEntity
{
    public Guid BatchId { get; set; }
    public FlockBatch Batch { get; set; } = null!;

    public DateOnly PlacementDate { get; set; }
    public int Quantity { get; set; }
    public decimal CostPerChick { get; set; }
    public decimal TotalCost => Quantity * CostPerChick;
    public string? SupplierName { get; set; }
    public string? InvoiceReference { get; set; }
    public string? Notes { get; set; }
}
