using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Infrastructure;

internal static class TestHostBuilderExtensions
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    internal static IWebHostBuilder UseInMemoryTradePilotPersistence(this IWebHostBuilder builder, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=fake;Database=fake;");
        builder.ConfigureServices(services =>
        {
            var efServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.RemoveAll<DbContextOptions<TradePilotDbContext>>();
            services.AddSingleton<DbContextOptions<TradePilotDbContext>>(
                new DbContextOptionsBuilder<TradePilotDbContext>()
                    .UseInMemoryDatabase(databaseName, DatabaseRoot)
                    .UseInternalServiceProvider(efServiceProvider)
                    .Options);
        });

        return builder;
    }
}