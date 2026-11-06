namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Breed/lineage reference data. Not tenant-scoped (shared reference table).
/// Examples: Molifoxin, BigBird, Cobb 500, Ross 308.
/// </summary>
public class Breed : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Lineage { get; set; }               // e.g., Molifoxin, BigBird
    public decimal TargetFcr { get; set; } = 1.8m;    // Target Feed Conversion Ratio
    public int TargetMarketAgedays { get; set; } = 35; // Target days to market weight
    public decimal TargetMarketWeightGrams { get; set; } = 2200m;
    public string? Notes { get; set; }

    // Navigation
    public ICollection<FlockBatch> Batches { get; set; } = new List<FlockBatch>();
}
