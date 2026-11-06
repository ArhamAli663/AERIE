namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// Core production unit: a group of chicks placed in a specific house on a specific date.
/// Tracks the full lifecycle from placement to sale/closure.
/// All daily records, mortality records, metrics, and marketplace listings reference this entity.
/// </summary>
public class FlockBatch : TenantEntity
{
    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public Guid PoultryHouseId { get; set; }
    public PoultryHouse PoultryHouse { get; set; } = null!;

    public Guid BreedId { get; set; }
    public Breed Breed { get; set; } = null!;

    public string BatchCode { get; set; } = string.Empty;   // Auto-generated e.g., BATCH-2024-001
    public DateOnly PlacementDate { get; set; }
    public int InitialChickCount { get; set; }
    public decimal CostPerChick { get; set; }
    public string? SupplierReference { get; set; }          // Chick supplier name/ref
    public BatchStatus Status { get; set; } = BatchStatus.Active;
    public string? Notes { get; set; }

    // Computed / cached fields (refreshed by background job)
    public int CurrentAliveCount { get; set; }
    public decimal TotalMortality { get; set; }             // Percentage
    public decimal? LatestAvgWeightGrams { get; set; }
    public decimal? CalculatedFcr { get; set; }

    // Navigation
    public ICollection<ChickPlacement> Placements { get; set; } = new List<ChickPlacement>();
    public ICollection<DailyFlockRecord> DailyRecords { get; set; } = new List<DailyFlockRecord>();
    public ICollection<MortalityRecord> MortalityRecords { get; set; } = new List<MortalityRecord>();
    public ICollection<ProductionMetric> ProductionMetrics { get; set; } = new List<ProductionMetric>();
    public ICollection<MarketplaceListing> Listings { get; set; } = new List<MarketplaceListing>();
}
