namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

public class MortalityRecord : TenantEntity
{
    public Guid BatchId { get; set; }
    public FlockBatch Batch { get; set; } = null!;
    public DateOnly RecordDate { get; set; }
    public int Count { get; set; }
    public string? Cause { get; set; }   // e.g., Disease, Heat Stress, Unknown
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
}
