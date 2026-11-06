namespace AERIE.Application;

using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Brokers.Services;
using AERIE.Application.Farmers.Interfaces;
using AERIE.Application.Farmers.Services;
using AERIE.Application.Sync.Interfaces;
using AERIE.Application.Sync.Services;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IFarmerService, FarmerService>();
        services.AddScoped<IBrokerService, BrokerService>();
        services.AddScoped<IDailyMarketRateService, DailyMarketRateService>();
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
