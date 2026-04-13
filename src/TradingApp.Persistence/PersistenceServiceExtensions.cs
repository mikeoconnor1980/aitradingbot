using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        var useSqlServer = IsSqlServerConnectionString(connectionString);

        services.AddDbContext<TradingAppDbContext>(options =>
        {
            if (useSqlServer)
                options.UseSqlServer(connectionString);
            else
                options.UseSqlite(connectionString);
        });

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
        services.AddScoped<ILlmContextSnapshotRepository, LlmContextSnapshotRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserWalletAddressRepository, UserWalletAddressRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();
        var connectionString = db.Database.GetConnectionString();

        if (connectionString is not null && !IsSqlServerConnectionString(connectionString))
        {
            // SQLite (local dev): ensure the data directory exists, then use EnsureCreated.
            // EnsureCreated is a no-op if the DB file already exists, so we also check for
            // missing tables and create them from the current model when the schema drifts.
            var csb = new SqliteConnectionStringBuilder(connectionString);
            var directory = Path.GetDirectoryName(Path.GetFullPath(csb.DataSource));

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var created = await db.Database.EnsureCreatedAsync();

            if (!created)
            {
                // DB already existed — check for missing tables and create them.
                await CreateMissingTablesAsync(db);
                await CreateMissingColumnsAsync(db);
            }
        }
        else
        {
            // SQL Server (production): apply migrations
            await db.Database.MigrateAsync();
        }
    }

    /// <summary>
    /// Compares tables declared in the EF model with tables that actually exist in the
    /// SQLite database, and creates any that are missing using EF's own DDL generation.
    /// </summary>
    private static async Task CreateMissingTablesAsync(TradingAppDbContext db)
    {
        var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new SqliteConnection(db.Database.GetConnectionString());
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingTables.Add(reader.GetString(0));
            }
        }

        // Check if any model tables are missing
        var modelTables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingTables = modelTables.Except(existingTables, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingTables.Count == 0)
            return;

        // Use EF's own script generator to get correct SQLite DDL
        var fullScript = db.Database.GenerateCreateScript();

        // Split into individual statements and execute only those for missing tables
        var statements = fullScript.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var statement in statements)
        {
            // Match CREATE TABLE "TableName" or CREATE INDEX ... ON "TableName"
            var isRelevant = missingTables.Any(t =>
                statement.Contains($"\"{t}\"", StringComparison.OrdinalIgnoreCase));

            if (!isRelevant)
                continue;

            await using var execCmd = conn.CreateCommand();
            execCmd.CommandText = statement;
            await execCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateMissingColumnsAsync(TradingAppDbContext db)
    {
        await using var conn = new SqliteConnection(db.Database.GetConnectionString());
        await conn.OpenAsync();

        var entityTypes = db.Model.GetEntityTypes()
            .Where(entityType => !string.IsNullOrWhiteSpace(entityType.GetTableName()));

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            var existingColumns = await GetExistingColumnsAsync(conn, tableName);

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(StoreObjectIdentifier.Table(tableName, null));
                if (string.IsNullOrWhiteSpace(columnName) || existingColumns.Contains(columnName))
                {
                    continue;
                }

                var columnType = GetSqliteColumnType(property);
                var nullability = property.IsNullable ? "NULL" : "NOT NULL";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnType} {nullability};";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<HashSet<string>> GetExistingColumnsAsync(SqliteConnection conn, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static string GetSqliteColumnType(IProperty property)
    {
        var providerType = property.GetProviderClrType() ?? property.ClrType;
        var underlyingType = Nullable.GetUnderlyingType(providerType) ?? providerType;

        if (underlyingType == typeof(int)
            || underlyingType == typeof(long)
            || underlyingType == typeof(short)
            || underlyingType == typeof(bool)
            || underlyingType.IsEnum)
        {
            return "INTEGER";
        }

        if (underlyingType == typeof(double)
            || underlyingType == typeof(float)
            || underlyingType == typeof(decimal))
        {
            return "REAL";
        }

        if (underlyingType == typeof(byte[]))
        {
            return "BLOB";
        }

        return "TEXT";
    }

    private static bool IsSqlServerConnectionString(string connectionString)
    {
        return connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Data Source=tcp:", StringComparison.OrdinalIgnoreCase);
    }
}
