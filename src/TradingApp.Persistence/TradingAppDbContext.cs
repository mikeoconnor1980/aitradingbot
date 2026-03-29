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
    public DbSet<FundingRate> FundingRates => Set<FundingRate>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();

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

            entity.Property(c => c.Source)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Hyperliquid");

            entity.HasIndex(c => new { c.Source, c.Symbol, c.Interval, c.Timestamp })
                .IsUnique()
                .HasDatabaseName("IX_Candles_Source_Symbol_Interval_Timestamp");

            // SQLite stores decimal values as REAL for query translation support.
            entity.Property(c => c.Open).HasConversion<double>();
            entity.Property(c => c.High).HasConversion<double>();
            entity.Property(c => c.Low).HasConversion<double>();
            entity.Property(c => c.Close).HasConversion<double>();
            entity.Property(c => c.Volume).HasConversion<double>();
        });

        modelBuilder.Entity<FundingRate>(entity =>
        {
            entity.ToTable("FundingRates");

            entity.HasKey(f => f.Id);

            entity.Property(f => f.Id)
                .ValueGeneratedOnAdd();

            entity.Property(f => f.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(f => f.Timestamp)
                .IsRequired();

            entity.Property(f => f.Rate)
                .HasConversion<double>()
                .IsRequired();

            entity.Property(f => f.MarkPrice)
                .HasConversion<double>()
                .IsRequired();

            entity.HasIndex(f => new { f.Symbol, f.Timestamp })
                .IsUnique()
                .HasDatabaseName("IX_FundingRates_Symbol_Timestamp");
        });

        modelBuilder.Entity<BacktestRun>(entity =>
        {
            entity.ToTable("BacktestRuns");

            entity.HasKey(backtestRun => backtestRun.Id);

            entity.Property(backtestRun => backtestRun.Id)
                .ValueGeneratedNever();

            entity.Property(backtestRun => backtestRun.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.IntervalsJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.StrategyConfigJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.TradesJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.EquityTimeSeriesJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.AuditLogEnabled);

            entity.Property(backtestRun => backtestRun.CandleLogJson);

            entity.Property(backtestRun => backtestRun.OrderEventLogJson);

            entity.Property(backtestRun => backtestRun.GridCycleLogJson);

            entity.Property(backtestRun => backtestRun.Status)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(backtestRun => backtestRun.InitialCapital)
                .HasConversion<double>();

            entity.Property(backtestRun => backtestRun.WinRate)
                .HasConversion<double>();

            entity.Property(backtestRun => backtestRun.TotalPnl)
                .HasConversion<double>();

            entity.Property(backtestRun => backtestRun.MaxDrawdown)
                .HasConversion<double>();

            entity.Property(backtestRun => backtestRun.AverageTradePnl)
                .HasConversion<double>();

            entity.Property(backtestRun => backtestRun.TotalFeesPaid)
                .HasConversion<double>();
        });
    }
}
