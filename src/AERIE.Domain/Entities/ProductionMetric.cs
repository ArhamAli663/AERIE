namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// Calculated production KPIs for a batch at a point in time.
/// Refreshed by a Hangfire background job after each daily record is submitted.
///
/// Formulas (configurable, defaults from poultry industry standards):
/// FCR = Total Feed Consumed (kg) / Total Live Weight Gain (kg)
/// ADG = (Current Avg Weight - Day 1 Weight) / Age in Days   [grams/day]
/// Mortality Rate = (Total Dead / Initial Chick Count) * 100
/// EPEF = ((Survival % * Avg Weight kg) / (FCR * Age Days)) * 100
/// </summary>
public class ProductionMetric : TenantEntity
{
    public Guid BatchId { get; set; }
    public FlockBatch Batch { get; set; } = null!;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public int AgeDays { get; set; }
    public decimal? Fcr { get; set; }                    // Feed Conversion Ratio
    public decimal? MortalityRatePct { get; set; }       // %
    public decimal? AdgGrams { get; set; }               // Average Daily Gain (g/day)
    public decimal? Epef { get; set; }                   // European Performance Efficiency Factor
    public decimal? TotalFeedKg { get; set; }
    public decimal? TotalLiveWeightKg { get; set; }
    public int SampleSize { get; set; }                  // Number of records in calculation
}
