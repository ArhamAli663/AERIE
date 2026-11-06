namespace AERIE.Domain.Services;

using AERIE.Domain.Entities;

/// <summary>
/// Pure domain service for calculating poultry production performance metrics.
/// No database dependencies — takes raw data and returns calculated values.
/// Formulas validated against industry standards (EPEF, FCR, ADG).
/// </summary>
public static class ProductionMetricsCalculator
{
    /// <summary>
    /// Feed Conversion Ratio = Total Feed Consumed (kg) / Total Live Weight Gain (kg)
    /// Lower is better. Industry target: ~1.7-1.9 for broilers.
    /// </summary>
    public static decimal? CalculateFcr(decimal totalFeedKg, decimal totalLiveWeightGainKg)
    {
        if (totalLiveWeightGainKg <= 0) return null;
        return Math.Round(totalFeedKg / totalLiveWeightGainKg, 3);
    }

    /// <summary>
    /// Average Daily Gain = (Current Avg Weight - Day 1 Body Weight) / Age in Days  [grams/day]
    /// </summary>
    public static decimal? CalculateAdg(decimal currentAvgWeightGrams, decimal day1WeightGrams, int ageDays)
    {
        if (ageDays <= 0) return null;
        return Math.Round((currentAvgWeightGrams - day1WeightGrams) / ageDays, 2);
    }

    /// <summary>
    /// Mortality Rate = (Total Dead / Initial Chick Count) * 100
    /// </summary>
    public static decimal CalculateMortalityRate(int totalDead, int initialCount)
    {
        if (initialCount <= 0) return 0;
        return Math.Round((decimal)totalDead / initialCount * 100, 2);
    }

    /// <summary>
    /// European Performance Efficiency Factor (EPEF):
    /// EPEF = (Survival % * Avg Weight kg / (FCR * Age Days)) * 100
    /// Higher is better. World-class broiler production: 400+
    /// </summary>
    public static decimal? CalculateEpef(decimal survivalPct, decimal avgWeightGrams, decimal fcr, int ageDays)
    {
        if (fcr <= 0 || ageDays <= 0) return null;
        var avgWeightKg = avgWeightGrams / 1000m;
        return Math.Round((survivalPct * avgWeightKg / (fcr * ageDays)) * 100, 1);
    }

    /// <summary>
    /// Calculates all metrics from a collection of daily records for a batch.
    /// </summary>
    public static (decimal? Fcr, decimal? Adg, decimal MortalityPct, decimal? Epef) CalculateAll(
        FlockBatch batch,
        IList<DailyFlockRecord> records)
    {
        if (!records.Any()) return (null, null, 0, null);

        var totalFeedKg = records.Sum(r => r.FeedConsumedKg);
        var latestRecord = records.OrderByDescending(r => r.RecordDate).First();
        var currentAlive = latestRecord.AliveCount > 0 ? latestRecord.AliveCount : Math.Max(0, batch.InitialChickCount - records.Sum(r => r.DailyMortality));
        var totalDead = batch.InitialChickCount > 0 ? Math.Max(0, batch.InitialChickCount - currentAlive) : records.Sum(r => r.DailyMortality);

        // Weight-based calculations
        var currentWeightGrams = latestRecord.AverageWeightGrams ?? 0;
        var day1WeightGrams = 40m; // Standard day-0 chick weight assumption (configurable)
        var totalLiveWeightKg = currentAlive * currentWeightGrams / 1000m;
        var initialBiomassKg = batch.InitialChickCount * day1WeightGrams / 1000m;
        var weightGainKg = Math.Max(0, totalLiveWeightKg - initialBiomassKg);

        var ageDays = (latestRecord.RecordDate.ToDateTime(TimeOnly.MinValue) -
                       batch.PlacementDate.ToDateTime(TimeOnly.MinValue)).Days;
        ageDays = Math.Max(1, ageDays);

        var fcr = CalculateFcr(totalFeedKg, weightGainKg);
        var adg = CalculateAdg(currentWeightGrams, day1WeightGrams, ageDays);
        var mortalityPct = CalculateMortalityRate(totalDead, batch.InitialChickCount);
        var survivalPct = 100m - mortalityPct;
        var epef = fcr.HasValue ? CalculateEpef(survivalPct, currentWeightGrams, fcr.Value, ageDays) : null;

        return (fcr, adg, mortalityPct, epef);
    }
}
