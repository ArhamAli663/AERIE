namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// An individual shed/barn within a farm where birds are housed.
/// Batches are assigned to a specific house.
/// </summary>
public class PoultryHouse : TenantEntity
{
    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;

    public string Name { get; set; } = string.Empty;       // e.g. "Shed A", "House 1"
    public int Capacity { get; set; }                        // Max birds
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<FlockBatch> Batches { get; set; } = new List<FlockBatch>();
}
