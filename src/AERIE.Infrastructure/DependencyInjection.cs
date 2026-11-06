namespace AERIE.Infrastructure;

using AERIE.Application.Auth.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Entities;
using AERIE.Infrastructure.Persistence;
using AERIE.Infrastructure.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=aerie_poultry_erp;Username=postgres;Password=postgres";

        var configuredProvider = configuration["DatabaseProvider"];
        bool usePostgreSql = false;

        if (!string.Equals(configuredProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync("localhost", 5432);
                if (connectTask.Wait(TimeSpan.FromMilliseconds(600)))
                {
                    usePostgreSql = true;
                }
            }
            catch
            {
                usePostgreSql = false;
            }
        }

        // ── EF Core with PostgreSQL or In-Memory Resilient Fallback ────────
        if (usePostgreSql)
        {
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(3);
                });
                options.EnableSensitiveDataLogging(false);
            });
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseInMemoryDatabase("aerie_poultry_erp");
                options.EnableSensitiveDataLogging(false);
            });
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ── Multi-Tenant Context & Auth ──────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IAuthService, AuthService>();

        // ── ASP.NET Core Identity ────────────────────────────────────────
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // ── Hangfire Background Jobs ──────────────────────────────────────
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                  .UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings();

            if (usePostgreSql)
            {
                config.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString));
            }
            else
            {
                config.UseMemoryStorage();
            }
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 8);
            options.Queues = ["metrics", "sync", "notifications", "default"];
        });

        // ── Automated Daily Market Rates Background Monitor ────────────────
        services.AddHostedService<DailyMarketRateBackgroundService>();

        return services;
    }
}

