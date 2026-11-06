namespace AERIE.Tests.Unit;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;
using FluentAssertions;
using Xunit;

public class BrokerFinancialCalculatorTests
{
    [Theory]
    [InlineData(2500.50, 340.00, 850170.00)]
    [InlineData(1000.00, 350.25, 350250.00)]
    [InlineData(0, 350.00, 0)]
    [InlineData(1500, 0, 0)]
    public void CalculateGrossValue_CalculatesAccuratelyWithRounding(decimal weightKg, decimal pricePerKg, decimal expectedGross)
    {
        var result = BrokerFinancialCalculator.CalculateGrossValue(weightKg, pricePerKg);
        result.Should().Be(expectedGross);
    }

    [Fact]
    public void CalculateCommission_PercentageRule_CalculatesCorrectly()
    {
        // 5,000 kg @ Rs 350 = 1,750,000 gross. Commission rate: 1.5%
        decimal gross = 1750000m;
        var commission = BrokerFinancialCalculator.CalculateCommission(gross, 5000m, 2500, CommissionRuleType.Percentage, 1.5m);
        commission.Should().Be(26250.00m);
    }

    [Fact]
    public void CalculateCommission_FlatPerBirdRule_CalculatesCorrectly()
    {
        // 2,500 birds @ Rs 3.50/bird
        var commission = BrokerFinancialCalculator.CalculateCommission(1750000m, 5000m, 2500, CommissionRuleType.FlatPerBird, 3.50m);
        commission.Should().Be(8750.00m);
    }

    [Fact]
    public void CalculateCommission_FlatPerKgRule_CalculatesCorrectly()
    {
        // 5,000 kg @ Rs 2.25/kg
        var commission = BrokerFinancialCalculator.CalculateCommission(1750000m, 5000m, 2500, CommissionRuleType.FlatPerKg, 2.25m);
        commission.Should().Be(11250.00m);
    }

    [Fact]
    public void CalculateNetFarmerPayable_DeductsCommissionFreightAndExpensesAccurately()
    {
        decimal gross = 1000000m;
        decimal commission = 15000m;
        decimal freight = 12000m;
        decimal otherDeductions = 3000m;
        decimal additions = 1500m; // Bonus or crate credit

        var netFarmer = BrokerFinancialCalculator.CalculateNetFarmerPayable(gross, commission, freight, otherDeductions, additions);
        // 1,000,000 - 15,000 - 12,000 - 3,000 + 1,500 = 971,500
        netFarmer.Should().Be(971500.00m);
    }

    [Fact]
    public void CalculateBrokerNetProfit_CalculatesAttributableMargin()
    {
        decimal earnedCommission = 25000m;
        decimal attributableExpenses = 4500m; // Driver allowance + inspection fee

        var profit = BrokerFinancialCalculator.CalculateBrokerNetProfit(earnedCommission, attributableExpenses);
        profit.Should().Be(20500.00m);
    }

    [Fact]
    public void CalculateShrinkage_CalculatesKgAndPercentageAccurately()
    {
        decimal farmWeight = 5000m;
        decimal mandiWeight = 4850m; // 150 kg shrinkage in transit

        var (shrinkageKg, shrinkagePct) = BrokerFinancialCalculator.CalculateShrinkage(farmWeight, mandiWeight);
        shrinkageKg.Should().Be(150.00m);
        shrinkagePct.Should().Be(3.00m);
    }

    [Fact]
    public void CalculateDiscrepancy_TracksTransitMortalityAndMissingBirds()
    {
        int loaded = 2000;
        int delivered = 1975;
        int transitMortality = 15;

        var (mortality, missing) = BrokerFinancialCalculator.CalculateDiscrepancy(loaded, delivered, transitMortality);
        mortality.Should().Be(15);
        missing.Should().Be(10); // 2000 - 1975 - 15 = 10 unaccounted
    }

    [Fact]
    public void CalculateArbitrageMargin_CalculatesSpreadAndNetMargin()
    {
        decimal sourceRate = 340.00m;      // Lahore
        decimal destinationRate = 368.00m; // Rawalpindi
        decimal freightRate = 12.00m;      // Transport cost per kg

        var margin = BrokerFinancialCalculator.CalculateArbitrageMargin(sourceRate, destinationRate, freightRate);
        // (368 - 340) - 12 = 16.00 Rs/kg
        margin.Should().Be(16.00m);
    }
}
