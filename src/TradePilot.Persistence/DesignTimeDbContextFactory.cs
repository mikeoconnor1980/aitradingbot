using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradePilot.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradePilotDbContext>
{
    public TradePilotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TradePilot_Design;Trusted_Connection=True;")
            .Options;

        return new TradePilotDbContext(options);
    }
}
