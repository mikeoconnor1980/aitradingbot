using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingApp.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradingAppDbContext>
{
    public TradingAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TradingApp_Design;Trusted_Connection=True;")
            .Options;

        return new TradingAppDbContext(options);
    }
}
