using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Persistence;

public sealed class TradePilotDbContext : DbContext
{
    public TradePilotDbContext(DbContextOptions<TradePilotDbContext> options)
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
    public DbSet<LiveOrder> LiveOrders => Set<LiveOrder>();
    public DbSet<LiveFill> LiveFills => Set<LiveFill>();
    public DbSet<GridCycle> GridCycles => Set<GridCycle>();
    public DbSet<LlmContextSnapshot> LlmContextSnapshots => Set<LlmContextSnapshot>();
    public DbSet<FearGreedReading> FearGreedReadings => Set<FearGreedReading>();
    public DbSet<AdminUserGrant> AdminUserGrants => Set<AdminUserGrant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserWalletAddress> UserWalletAddresses => Set<UserWalletAddress>();
    public DbSet<WebhookConfig> WebhookConfigs => Set<WebhookConfig>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();
    public DbSet<TelegramLinkCode> TelegramLinkCodes => Set<TelegramLinkCode>();
    public DbSet<StrategyTemplate> StrategyTemplates => Set<StrategyTemplate>();

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

            // Decimal values stored as double for legacy compatibility.
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

            entity.Property(backtestRun => backtestRun.Expectancy)
                .HasConversion<double?>();

            entity.Property(backtestRun => backtestRun.ProfitFactor)
                .HasConversion<double?>();

            entity.Property(backtestRun => backtestRun.Sqn)
                .HasConversion<double?>();

            entity.Property(backtestRun => backtestRun.KellyPercent)
                .HasConversion<double?>();

            entity.Property(backtestRun => backtestRun.HalfKellyPercent)
                .HasConversion<double?>();

            entity.Property(backtestRun => backtestRun.WinLossRRatio)
                .HasConversion<double?>();

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

            entity.Property(strategy => strategy.AssignedAgentId)
                .HasMaxLength(100);

            entity.Property(strategy => strategy.LastStartedAtUtc);

            entity.Property(strategy => strategy.LastStoppedAtUtc);

            entity.Property(strategy => strategy.CreatedAtUtc)
                .IsRequired();

            entity.Property(strategy => strategy.UpdatedAtUtc)
                .IsRequired();

            entity.Property(strategy => strategy.HighWaterMarkUsd)
                .HasColumnName("HighWaterMarkUsd")
                .HasConversion<double?>();

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

        modelBuilder.Entity<LiveOrder>(entity =>
        {
            entity.ToTable("LiveOrders");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.OrderId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.GridCycleId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Side)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.OrderType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Price)
                .HasConversion<double>();

            entity.Property(e => e.Size)
                .HasConversion<double>();

            entity.Property(e => e.TradeType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.OrderId)
                .IsUnique()
                .HasDatabaseName("IX_LiveOrders_OrderId");

            entity.HasIndex(e => e.GridCycleId)
                .HasDatabaseName("IX_LiveOrders_GridCycleId");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_LiveOrders_UserId");
        });

        modelBuilder.Entity<LiveFill>(entity =>
        {
            entity.ToTable("LiveFills");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.OrderId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Side)
                .HasMaxLength(10)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.Direction)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Price)
                .HasConversion<double>();

            entity.Property(e => e.Size)
                .HasConversion<double>();

            entity.Property(e => e.Fee)
                .HasConversion<double>();

            entity.Property(e => e.ClosedPnl)
                .HasConversion<double>();

            entity.Property(e => e.UserId)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("IX_LiveFills_OrderId");

            entity.HasIndex(e => new { e.Symbol, e.FilledAtUtc })
                .HasDatabaseName("IX_LiveFills_Symbol_FilledAtUtc");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_LiveFills_UserId");
        });

        modelBuilder.Entity<GridCycle>(entity =>
        {
            entity.ToTable("GridCycles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.GridCycleId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.StrategyName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.AnchorPrice)
                .HasConversion<double>();

            entity.Property(e => e.Lifecycle)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.CloseReason)
                .HasMaxLength(50);

            entity.Property(e => e.RealisedPnl)
                .HasConversion<double?>();

            entity.Property(e => e.UserId)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.GridCycleId)
                .IsUnique()
                .HasDatabaseName("IX_GridCycles_GridCycleId");

            entity.HasIndex(e => new { e.StrategyName, e.Symbol, e.Lifecycle })
                .HasDatabaseName("IX_GridCycles_Strategy_Symbol_Lifecycle");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_GridCycles_UserId");
        });

        modelBuilder.Entity<LlmContextSnapshot>(entity =>
        {
            entity.ToTable("LlmContextSnapshots");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.MarketSentiment)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.MacroRegime)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.EventRisk)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Confidence)
                .HasConversion<double>();

            entity.Property(e => e.DerivedRegime)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Summary)
                .HasMaxLength(1000);

            entity.HasIndex(e => new { e.Symbol, e.GeneratedAtUtc })
                .HasDatabaseName("IX_LlmContextSnapshots_Symbol_GeneratedAtUtc");

            entity.HasIndex(e => e.GeneratedAtUtc)
                .HasDatabaseName("IX_LlmContextSnapshots_GeneratedAtUtc");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .IsRequired(false);

            entity.Property(e => e.AuthProvider)
                .HasMaxLength(20)
                .IsRequired(false);

            entity.Property(e => e.ExternalProviderId)
                .HasMaxLength(256)
                .IsRequired(false);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.Property(e => e.PreferredNetwork)
                .HasMaxLength(10)
                .IsRequired()
                .HasDefaultValue("mainnet");

            entity.Property(e => e.TelegramChatId)
                .IsRequired(false);

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.HasIndex(e => new { e.AuthProvider, e.ExternalProviderId })
                .IsUnique()
                .HasFilter("[AuthProvider] IS NOT NULL AND [ExternalProviderId] IS NOT NULL")
                .HasDatabaseName("IX_Users_ExternalProvider");
        });

        modelBuilder.Entity<AdminUserGrant>(entity =>
        {
            entity.ToTable("AdminUserGrants");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_AdminUserGrants_Email");
        });

        modelBuilder.Entity<UserWalletAddress>(entity =>
        {
            entity.ToTable("UserWalletAddresses");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.Exchange)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.WalletAddress)
                .HasMaxLength(42)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.IsActive })
                .HasDatabaseName("IX_UserWalletAddresses_UserId_IsActive");
        });

        modelBuilder.Entity<WebhookConfig>(entity =>
        {
            entity.ToTable("WebhookConfigs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Label)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(e => e.Token)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.DefaultAsset)
                .HasMaxLength(20);

            entity.Property(e => e.TargetAgentId)
                .HasMaxLength(120);

            entity.HasIndex(e => e.Token)
                .IsUnique()
                .HasDatabaseName("IX_WebhookConfigs_Token");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_WebhookConfigs_UserId");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.Tier)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired();

            entity.Property(e => e.StartedAtUtc)
                .IsRequired();

            entity.Property(e => e.ExpiresAtUtc)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Subscriptions_UserId");
        });

        modelBuilder.Entity<ExecutionLog>(entity =>
        {
            entity.ToTable("ExecutionLogs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.AgentId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.TimestampUtc)
                .IsRequired();

            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.Level)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.Data)
                .HasMaxLength(4000);

            entity.Property(e => e.ReceivedAtUtc)
                .IsRequired();

            entity.HasIndex(e => new { e.AgentId, e.TimestampUtc })
                .HasDatabaseName("IX_ExecutionLogs_AgentId_TimestampUtc");

            entity.HasIndex(e => e.ReceivedAtUtc)
                .HasDatabaseName("IX_ExecutionLogs_ReceivedAtUtc");
        });

        modelBuilder.Entity<TelegramLinkCode>(entity =>
        {
            entity.ToTable("TelegramLinkCodes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.Code)
                .HasMaxLength(6)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.Property(e => e.ExpiresAtUtc)
                .IsRequired();

            entity.Property(e => e.IsUsed)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Code)
                .HasFilter("[IsUsed] = 0")
                .HasDatabaseName("IX_TelegramLinkCodes_Code");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_TelegramLinkCodes_UserId");
        });

        modelBuilder.Entity<FearGreedReading>(entity =>
        {
            entity.ToTable("FearGreedReadings");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Value).IsRequired();

            entity.Property(e => e.Classification)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(e => e.Timestamp)
                .IsUnique()
                .HasDatabaseName("IX_FearGreedReadings_Timestamp");
        });

        modelBuilder.Entity<StrategyTemplate>(entity =>
        {
            entity.ToTable("StrategyTemplates");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id)
                .ValueGeneratedNever();

            entity.Property(t => t.Slug)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(t => t.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(t => t.Description)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(t => t.StrategyMode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.Direction)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.Market)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.TagsJson)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(t => t.ConfigJson)
                .IsRequired();

            entity.Property(t => t.SortOrder)
                .IsRequired();

            entity.Property(t => t.IsSystemTemplate)
                .IsRequired();

            entity.Property(t => t.IsBeginnerVisible)
                .IsRequired();

            entity.Property(t => t.IsActive)
                .IsRequired();

            entity.Property(t => t.CreatedAtUtc)
                .IsRequired();

            entity.Property(t => t.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(t => t.Slug)
                .IsUnique()
                .HasDatabaseName("IX_StrategyTemplates_Slug");

            entity.HasIndex(t => t.Name)
                .IsUnique()
                .HasDatabaseName("IX_StrategyTemplates_Name")
                .HasFilter("[IsActive] = 1");

            entity.HasIndex(t => new { t.IsActive, t.SortOrder })
                .HasDatabaseName("IX_StrategyTemplates_IsActive_SortOrder");
        });
    }
}
