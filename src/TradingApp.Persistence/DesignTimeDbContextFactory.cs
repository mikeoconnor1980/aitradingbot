using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingApp.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradingAppDbContext>
{
    public TradingAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlite("Data Source=Data/tradingapp.db")
            .Options;

        return new TradingAppDbContext(options);
    }
}
