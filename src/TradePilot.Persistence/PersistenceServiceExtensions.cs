using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Persistence.Repositories;
using TradePilot.Persistence.Seeding;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.TradeJournal.Services;

namespace TradePilot.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Add it to appsettings.json.");

        services.AddDbContext<TradePilotDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
        services.AddScoped<IOptimizationRunRepository, OptimizationRunRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<IFundingRateRepository, FundingRateRepository>();
        services.AddScoped<IStrategyRepository, StrategyRepository>();
        services.AddScoped<IStrategyRevisionRepository, StrategyRevisionRepository>();
        services.AddScoped<IStrategyEvaluationRepository, StrategyEvaluationRepository>();
        services.AddScoped<ITradeJournalRepository, TradeJournalRepository>();
        services.AddScoped<ITradeJournalService, TradeJournalService>();
        services.AddScoped<IStrategyReviewRepository, StrategyReviewRepository>();
        services.AddScoped<IStrategyTemplateRepository, StrategyTemplateRepository>();
        services.AddScoped<ILiveOrderRepository, LiveOrderRepository>();
        services.AddScoped<ILiveFillRepository, LiveFillRepository>();
        services.AddScoped<IGridCycleRepository, GridCycleRepository>();
        services.AddScoped<ILlmContextSnapshotRepository, LlmContextSnapshotRepository>();
        services.AddScoped<IAdminUserGrantRepository, AdminUserGrantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserWalletAddressRepository, UserWalletAddressRepository>();
        services.AddScoped<IUserExchangeCredentialRepository, UserExchangeCredentialRepository>();
        services.AddScoped<IWebhookConfigRepository, WebhookConfigRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IFearGreedReadingRepository, FearGreedReadingRepository>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();

        // InMemory provider (used in tests) doesn't support migrations
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            await db.Database.EnsureCreatedAsync();
            await AdminUserGrantSeeder.SeedAsync(db);
            return;
        }

        await db.Database.MigrateAsync();
        await AdminUserGrantSeeder.SeedAsync(db);
        await StrategyTemplateSeeder.SeedAsync(db);
    }
}
