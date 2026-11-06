namespace AERIE.Tests.Unit;

using AERIE.Domain.Entities;
using AERIE.Domain.Services;
using FluentAssertions;
using Xunit;

public class ProductionMetricsCalculatorTests
{
    [Fact]
    public void CalculateFcr_WithValidFeedAndWeightGain_ReturnsAccurateRatio()
    {
        // Arrange: 3,500 kg feed consumed for 2,000 kg weight gain
        decimal totalFeedKg = 3500m;
        decimal totalWeightGainKg = 2000m;

        // Act
        var fcr = ProductionMetricsCalculator.CalculateFcr(totalFeedKg, totalWeightGainKg);

        // Assert: 3500 / 2000 = 1.75
        fcr.Should().NotBeNull();
        fcr.Should().Be(1.750m);
    }

    [Fact]
    public void CalculateFcr_WithZeroWeightGain_ReturnsNull()
    {
        // Act
        var fcr = ProductionMetricsCalculator.CalculateFcr(1000m, 0m);

        // Assert
        fcr.Should().BeNull();
    }

    [Fact]
    public void CalculateAdg_WithValidWeightsAndDays_ReturnsDailyGainInGrams()
    {
        // Arrange: Day 1 chick weight = 42g, Day 35 harvest weight = 2100g, Age = 35 days
        decimal currentWeight = 2100m;
        decimal day1Weight = 42m;
        int ageDays = 35;

        // Act
        var adg = ProductionMetricsCalculator.CalculateAdg(currentWeight, day1Weight, ageDays);

        // Assert: (2100 - 42) / 35 = 58.80 g/day
        adg.Should().NotBeNull();
        adg.Should().Be(58.80m);
    }

    [Fact]
    public void CalculateAdg_WithZeroDays_ReturnsNull()
    {
        // Act
        var adg = ProductionMetricsCalculator.CalculateAdg(500m, 40m, 0);

        // Assert
        adg.Should().BeNull();
    }

    [Theory]
    [InlineData(100, 5000, 2.00)]
    [InlineData(250, 5000, 5.00)]
    [InlineData(0, 5000, 0.00)]
    public void CalculateMortalityRate_WithVariousCounts_ReturnsExpectedPercentage(
        int totalDead, int initialCount, decimal expectedRate)
    {
        // Act
        var rate = ProductionMetricsCalculator.CalculateMortalityRate(totalDead, initialCount);

        // Assert
        rate.Should().Be(expectedRate);
    }

    [Fact]
    public void CalculateMortalityRate_WithZeroInitialCount_ReturnsZero()
    {
        // Act
        var rate = ProductionMetricsCalculator.CalculateMortalityRate(10, 0);

        // Assert
        rate.Should().Be(0m);
    }

    [Fact]
    public void CalculateEpef_WithStandardBroilerData_ReturnsExpectedFactor()
    {
        // Arrange: Survival = 96.5%, Avg Weight = 2150g, FCR = 1.72, Age = 35 days
        decimal survivalPct = 96.5m;
        decimal avgWeightGrams = 2150m; // 2.150 kg
        decimal fcr = 1.72m;
        int ageDays = 35;

        // Act
        // EPEF = (96.5 * 2.150 / (1.72 * 35)) * 100 = (207.475 / 60.2) * 100 = 344.6
        var epef = ProductionMetricsCalculator.CalculateEpef(survivalPct, avgWeightGrams, fcr, ageDays);

        // Assert
        epef.Should().NotBeNull();
        epef.Should().Be(344.6m);
    }

    [Fact]
    public void CalculateAll_WithFullFlockHistory_ReturnsComprehensiveMetrics()
    {
        // Arrange
        var batch = new FlockBatch
        {
            InitialChickCount = 1000,
            CurrentAliveCount = 950,
            PlacementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35))
        };

        var records = new List<DailyFlockRecord>
        {
            new() { RecordDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)), AliveCount = 990, DailyMortality = 10, FeedConsumedKg = 25m, AverageWeightGrams = 80m },
            new() { RecordDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)), AliveCount = 970, DailyMortality = 20, FeedConsumedKg = 700m, AverageWeightGrams = 900m },
            new() { RecordDate = DateOnly.FromDateTime(DateTime.UtcNow), AliveCount = 950, DailyMortality = 20, FeedConsumedKg = 1000m, AverageWeightGrams = 2000m }
        };

        // Act
        var (fcr, adg, mortalityPct, epef) = ProductionMetricsCalculator.CalculateAll(batch, records);

        // Assert
        mortalityPct.Should().Be(5.00m); // 50 dead / 1000 placed = 5%
        fcr.Should().NotBeNull();
        adg.Should().NotBeNull();
        epef.Should().NotBeNull();
    }
}
