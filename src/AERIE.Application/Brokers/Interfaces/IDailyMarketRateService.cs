namespace AERIE.Application.Brokers.Interfaces;

using AERIE.Application.Common.Models;

public interface IDailyMarketRateService
{
    /// <summary>
    /// Checks if today's market rates are present in the database.
    /// If today's rates do not exist, automatically generates realistic daily updated market rates for all 26 cities.
    /// </summary>
    Task<Result<int>> EnsureDailyRatesCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Triggers an immediate daily price update/fluctuation across all 26 Pakistan poultry markets,
    /// updating rates, 24h trends, and synchronized poultry shop retail and live rates.
    /// </summary>
    Task<Result<int>> RefreshDailyMarketPricesAsync(bool forceNewFluctuation = false, CancellationToken ct = default);
}
