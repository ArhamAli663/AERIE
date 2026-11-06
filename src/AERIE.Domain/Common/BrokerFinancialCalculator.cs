namespace AERIE.Domain.Common;

using AERIE.Domain.Enums;

/// <summary>
/// Centralized financial calculation engine for poultry brokerage transactions.
/// Pure deterministic functions using decimal arithmetic and explicit rounding.
/// </summary>
public static class BrokerFinancialCalculator
{
    public static decimal CalculateGrossValue(decimal weightKg, decimal pricePerKg)
    {
        if (weightKg <= 0 || pricePerKg <= 0) return 0m;
        return Math.Round(weightKg * pricePerKg, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateCommission(
        decimal grossValue,
        decimal weightKg,
        int birdCount,
        CommissionRuleType ruleType,
        decimal rate)
    {
        if (rate <= 0) return 0m;

        decimal amount = ruleType switch
        {
            CommissionRuleType.Percentage => grossValue * (rate / 100m),
            CommissionRuleType.FlatPerBird => birdCount * rate,
            CommissionRuleType.FlatPerKg => weightKg * rate,
            _ => 0m
        };

        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateNetFarmerPayable(
        decimal grossValue,
        decimal commissionAmount,
        decimal freightAmount,
        decimal otherDeductions = 0m,
        decimal additions = 0m)
    {
        var net = grossValue - commissionAmount - freightAmount - otherDeductions + additions;
        return Math.Round(Math.Max(0m, net), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateBuyerReceivable(
        decimal grossValue,
        decimal buyerCommission = 0m,
        decimal buyerFreight = 0m)
    {
        var total = grossValue + buyerCommission + buyerFreight;
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateBrokerGrossMargin(decimal commissionEarned, decimal otherBrokerFees = 0m)
    {
        return Math.Round(commissionEarned + otherBrokerFees, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateBrokerNetProfit(decimal commissionEarned, decimal attributableExpenses)
    {
        var net = commissionEarned - attributableExpenses;
        return Math.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    public static (decimal ShrinkageKg, decimal ShrinkagePct) CalculateShrinkage(
        decimal weightLiftedKg,
        decimal weightDeliveredKg)
    {
        if (weightLiftedKg <= 0 || weightDeliveredKg <= 0) return (0m, 0m);
        var diff = Math.Max(0m, weightLiftedKg - weightDeliveredKg);
        var pct = (diff / weightLiftedKg) * 100m;
        return (
            Math.Round(diff, 2, MidpointRounding.AwayFromZero),
            Math.Round(pct, 2, MidpointRounding.AwayFromZero)
        );
    }

    public static (int MortalityCount, int MissingBirdsCount) CalculateDiscrepancy(
        int birdsLoaded,
        int birdsDelivered,
        int transitMortality)
    {
        if (birdsLoaded <= 0) return (transitMortality, 0);
        var missing = Math.Max(0, birdsLoaded - birdsDelivered - transitMortality);
        return (transitMortality, missing);
    }

    public static decimal CalculateArbitrageMargin(
        decimal sourcePricePerKg,
        decimal destinationPricePerKg,
        decimal freightPerKg)
    {
        var spread = destinationPricePerKg - sourcePricePerKg;
        return Math.Round(spread - freightPerKg, 2, MidpointRounding.AwayFromZero);
    }
}
