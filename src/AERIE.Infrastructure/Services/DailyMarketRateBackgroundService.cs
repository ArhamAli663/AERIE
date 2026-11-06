namespace AERIE.Infrastructure.Services;

using AERIE.Application.Brokers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DailyMarketRateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyMarketRateBackgroundService> _logger;

    public DailyMarketRateBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DailyMarketRateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DailyMarketRateBackgroundService] Automated daily poultry market rate monitor started.");

        // Initial check on application boot to ensure today's rates exist
        await CheckAndUpdateRatesAsync(stoppingToken);

        // Periodically verify every 15 minutes to automatically roll rates when a new calendar day arrives
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndUpdateRatesAsync(stoppingToken);
        }
    }

    private async Task CheckAndUpdateRatesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var rateService = scope.ServiceProvider.GetRequiredService<IDailyMarketRateService>();
            var result = await rateService.EnsureDailyRatesCurrentAsync(stoppingToken);
            if (result.IsSuccess && result.Value > 0)
            {
                _logger.LogInformation("[DailyMarketRateBackgroundService] Successfully generated {Count} fresh daily market rates.", result.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DailyMarketRateBackgroundService] Error during automated daily rate verification: {Message}", ex.Message);
        }
    }
}
