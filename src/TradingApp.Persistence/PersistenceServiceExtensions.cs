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

        services.AddScoped<ICandleRepository, CandleRepository>();

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
