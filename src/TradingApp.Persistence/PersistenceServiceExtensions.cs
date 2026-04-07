using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Persistence.Repositories;

namespace TradingApp.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Add it to appsettings.json.");

        services.AddDbContext<TradingAppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
        services.AddScoped<IOptimizationRunRepository, OptimizationRunRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<IFundingRateRepository, FundingRateRepository>();
        services.AddScoped<IStrategyRepository, StrategyRepository>();
        services.AddScoped<IStrategyRevisionRepository, StrategyRevisionRepository>();
        services.AddScoped<IStrategyReviewRepository, StrategyReviewRepository>();
        services.AddScoped<ILiveOrderRepository, LiveOrderRepository>();
        services.AddScoped<ILiveFillRepository, LiveFillRepository>();
        services.AddScoped<IGridCycleRepository, GridCycleRepository>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();
        var connectionString = db.Database.GetConnectionString();

        if (connectionString is not null)
        {
            var csb = new SqliteConnectionStringBuilder(connectionString);
            var directory = Path.GetDirectoryName(Path.GetFullPath(csb.DataSource));

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
        }

        await db.Database.MigrateAsync();
    }
}
