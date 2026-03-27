using Microsoft.EntityFrameworkCore;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence;

public sealed class TradingAppDbContext : DbContext
{
    public TradingAppDbContext(DbContextOptions<TradingAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Candle> Candles => Set<Candle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Candle>(entity =>
        {
            entity.ToTable("Candles");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .ValueGeneratedOnAdd();

            entity.Property(c => c.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(c => c.Interval)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(c => new { c.Symbol, c.Interval, c.Timestamp })
                .IsUnique()
                .HasDatabaseName("IX_Candles_Symbol_Interval_Timestamp");

            // SQLite stores decimal values as REAL for query translation support.
            entity.Property(c => c.Open).HasConversion<double>();
            entity.Property(c => c.High).HasConversion<double>();
            entity.Property(c => c.Low).HasConversion<double>();
            entity.Property(c => c.Close).HasConversion<double>();
            entity.Property(c => c.Volume).HasConversion<double>();
        });
    }
}
