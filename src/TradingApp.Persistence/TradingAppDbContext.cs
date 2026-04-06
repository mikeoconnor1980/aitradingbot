using Microsoft.EntityFrameworkCore;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

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
    public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
    public DbSet<OptimizationResult> OptimizationResults => Set<OptimizationResult>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyRevision> StrategyRevisions => Set<StrategyRevision>();
    public DbSet<StrategyReview> StrategyReviews => Set<StrategyReview>();
    public DbSet<MacroEvent> MacroEvents => Set<MacroEvent>();
    public DbSet<MacroSyncRun> MacroSyncRuns => Set<MacroSyncRun>();

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

            entity.Property(backtestRun => backtestRun.ExecutionConfigJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.TradesJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.EquityTimeSeriesJson)
                .IsRequired();

            entity.Property(backtestRun => backtestRun.AuditLogEnabled);

            entity.Property(backtestRun => backtestRun.CandleLogJson);

            entity.Property(backtestRun => backtestRun.OrderEventLogJson);

            entity.Property(backtestRun => backtestRun.GridCycleLogJson);

            entity.Property(backtestRun => backtestRun.StrategyId);

            entity.Property(backtestRun => backtestRun.StrategyRevisionId);

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

            entity.HasIndex(backtestRun => backtestRun.StrategyId)
                .HasDatabaseName("IX_BacktestRuns_StrategyId");
        });

        modelBuilder.Entity<OptimizationRun>(entity =>
        {
            entity.ToTable("OptimizationRuns");

            entity.HasKey(run => run.Id);

            entity.Property(run => run.Id)
                .ValueGeneratedNever();

            entity.Property(run => run.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(run => run.SweepConfigJson)
                .IsRequired();

            entity.Property(run => run.ThresholdsJson)
                .IsRequired();

            entity.Property(run => run.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(run => run.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(run => run.InitialCapital)
                .HasConversion<double>();

            entity.HasIndex(run => run.CreatedAtUtc)
                .HasDatabaseName("IX_OptimizationRuns_CreatedAtUtc");
        });

        modelBuilder.Entity<OptimizationResult>(entity =>
        {
            entity.ToTable("OptimizationResults");

            entity.HasKey(result => result.Id);

            entity.Property(result => result.Id)
                .ValueGeneratedNever();

            entity.Property(result => result.StrategyConfigJson)
                .IsRequired();

            entity.Property(result => result.SignalDescription)
                .IsRequired();

            entity.Property(result => result.FitnessScore)
                .HasConversion<double>();

            entity.Property(result => result.TotalPnl)
                .HasConversion<double>();

            entity.Property(result => result.WinRate)
                .HasConversion<double>();

            entity.Property(result => result.MaxDrawdown)
                .HasConversion<double>();

            entity.Property(result => result.TotalFeesPaid)
                .HasConversion<double>();

            entity.Property(result => result.AverageTradePnl)
                .HasConversion<double>();

            entity.Property(result => result.OosTotalPnl)
                .HasConversion<double?>();

            entity.Property(result => result.OosWinRate)
                .HasConversion<double?>();

            entity.Property(result => result.OosMaxDrawdown)
                .HasConversion<double?>();

            entity.Property(result => result.OosFitnessScore)
                .HasConversion<double?>();

            entity.Property(result => result.SharpeRatio)
                .HasConversion<double?>();

            entity.Property(result => result.SortinoRatio)
                .HasConversion<double?>();

            entity.Property(result => result.ProfitFactor)
                .HasConversion<double?>();

            entity.Property(result => result.CalmarRatio)
                .HasConversion<double?>();

            entity.HasOne<OptimizationRun>()
                .WithMany()
                .HasForeignKey(result => result.OptimizationRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(result => result.OptimizationRunId)
                .HasDatabaseName("IX_OptimizationResults_RunId");

            entity.HasIndex(result => new { result.OptimizationRunId, result.Rank })
                .IsUnique()
                .HasDatabaseName("IX_OptimizationResults_RunId_Rank");
        });

        modelBuilder.Entity<Strategy>(entity =>
        {
            entity.ToTable("Strategies");

            entity.HasKey(strategy => strategy.Id);

            entity.Property(strategy => strategy.Id)
                .ValueGeneratedNever();

            entity.Property(strategy => strategy.UserId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(strategy => strategy.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(strategy => strategy.StrategyType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(strategy => strategy.ConfigJson)
                .IsRequired();

            entity.Property(strategy => strategy.Version)
                .IsRequired();

            entity.Property(strategy => strategy.IsActive)
                .IsRequired();

            entity.Property(strategy => strategy.IsRunning)
                .IsRequired();

            entity.Property(strategy => strategy.CreatedAtUtc)
                .IsRequired();

            entity.Property(strategy => strategy.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(strategy => new { strategy.UserId, strategy.IsActive })
                .HasDatabaseName("IX_Strategies_UserId_IsActive");

            entity.HasIndex(strategy => new { strategy.UserId, strategy.Name })
                .IsUnique()
                .HasDatabaseName("IX_Strategies_UserId_Name")
                .HasFilter("[IsActive] = 1");
        });

        modelBuilder.Entity<StrategyRevision>(entity =>
        {
            entity.ToTable("StrategyRevisions");

            entity.HasKey(revision => revision.Id);

            entity.Property(revision => revision.Id)
                .ValueGeneratedNever();

            entity.Property(revision => revision.StrategyId)
                .IsRequired();

            entity.Property(revision => revision.RevisionNumber)
                .IsRequired();

            entity.Property(revision => revision.ConfigJson)
                .IsRequired();

            entity.Property(revision => revision.Source)
                .HasMaxLength(20)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(revision => revision.Label)
                .HasMaxLength(200);

            entity.Property(revision => revision.ChangeSummary)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(revision => revision.CreatedAtUtc)
                .IsRequired();

            entity.HasOne<Strategy>()
                .WithMany()
                .HasForeignKey(revision => revision.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(revision => new { revision.StrategyId, revision.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("IX_StrategyRevisions_StrategyId_RevisionNumber");
        });

        modelBuilder.Entity<StrategyReview>(entity =>
        {
            entity.ToTable("StrategyReviews");

            entity.HasKey(review => review.Id);

            entity.Property(review => review.Id)
                .ValueGeneratedNever();

            entity.Property(review => review.StrategyId)
                .IsRequired();

            entity.Property(review => review.RevisionNumber)
                .IsRequired();

            entity.Property(review => review.ReviewMarkdown)
                .IsRequired();

            entity.Property(review => review.ModelName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(review => review.CreatedAtUtc)
                .IsRequired();

            entity.Property(review => review.IsFallback)
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasOne<Strategy>()
                .WithMany()
                .HasForeignKey(review => review.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(review => new { review.StrategyId, review.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("IX_StrategyReviews_StrategyId_RevisionNumber");
        });

        modelBuilder.Entity<MacroEvent>(entity =>
        {
            entity.ToTable("MacroEvents");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ProviderEventId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Importance)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired();

            entity.Property(e => e.RawPayloadJson)
                .HasMaxLength(4000);

            entity.HasIndex(e => new { e.Provider, e.ProviderEventId })
                .IsUnique()
                .HasDatabaseName("IX_MacroEvents_Provider_ProviderEventId");

            entity.HasIndex(e => e.ScheduledAtUtc)
                .HasDatabaseName("IX_MacroEvents_ScheduledAtUtc");

            entity.HasIndex(e => e.BlockStartUtc)
                .HasDatabaseName("IX_MacroEvents_BlockStartUtc");

            entity.HasIndex(e => e.BlockEndUtc)
                .HasDatabaseName("IX_MacroEvents_BlockEndUtc");

            entity.HasIndex(e => e.Importance)
                .HasDatabaseName("IX_MacroEvents_Importance");
        });

        modelBuilder.Entity<MacroSyncRun>(entity =>
        {
            entity.ToTable("MacroSyncRuns");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Error)
                .HasMaxLength(2000);

            entity.HasIndex(e => e.StartedAtUtc)
                .HasDatabaseName("IX_MacroSyncRuns_StartedAtUtc");
        });
    }
}
