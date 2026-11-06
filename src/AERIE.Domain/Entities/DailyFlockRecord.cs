namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Daily observation log for a flock batch: mortality, feed consumption,
/// water usage and average weight. This is the primary offline-captured record
/// from the Flutter mobile app.
/// </summary>
public class DailyFlockRecord : TenantEntity
{
    public Guid BatchId { get; set; }
    public FlockBatch Batch { get; set; } = null!;

    public DateOnly RecordDate { get; set; }
    public int OpeningCount { get; set; }
    public int AliveCount { get; set; }
    public int ClosingCount { get; set; }
    public int DailyMortality { get; set; }
    public string? FeedCategory { get; set; } = "Starter"; // Pre-Starter, Starter, Grower, Finisher
    public decimal FeedConsumedKg { get; set; }
    public decimal WaterConsumedLiters { get; set; }
    public decimal? AverageWeightGrams { get; set; }
    public decimal? AverageFeedPerBirdGrams { get; set; }
    public string? Notes { get; set; }

    // Mortality threshold alert metadata
    public bool IsAbnormalMortality { get; set; }
    public string? MortalityAlertReason { get; set; }

    // Recorded by (user reference — FK to ApplicationUser.Id)
    public Guid? RecordedByUserId { get; set; }

    // Sync metadata (set by mobile app, verified server-side)
    public string? ClientRecordId { get; set; }   // Stable UUID from mobile for idempotency
    public int RowVersion { get; set; } = 1;       // For conflict detection
}
