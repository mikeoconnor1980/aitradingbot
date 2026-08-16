using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

/// <summary>
/// Durable historical evidence for one logical strategy position, from its first opening fill until flat.
/// </summary>
public sealed class TradeJournalRecord
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid? StrategyId { get; private set; }
    public string StrategyName { get; private set; } = string.Empty;
    public int? StrategyVersion { get; private set; }
    public string ConfigurationIdentity { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public TradeSide Side { get; private set; }
    public TradeLifecycleStatus Status { get; private set; }
    public DateTime EntryTimeUtc { get; private set; }
    public DateTime? ExitTimeUtc { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal? ExitPrice { get; private set; }
    public decimal EntryQuantity { get; private set; }
    public decimal ExitQuantity { get; private set; }
    public decimal? Leverage { get; private set; }
    public decimal GrossPnl { get; private set; }
    public decimal Fees { get; private set; }
    public decimal? Funding { get; private set; }
    public decimal NetPnl { get; private set; }
    public long? DurationMilliseconds { get; private set; }
    public decimal? MfeAmount { get; private set; }
    public decimal? MfePercent { get; private set; }
    public decimal? MaeAmount { get; private set; }
    public decimal? MaePercent { get; private set; }
    public Guid? EntryStrategyEvaluationId { get; private set; }
    public Guid? ExitStrategyEvaluationId { get; private set; }
    public TradeExitReason ExitReason { get; private set; }
    public string? EntryMarketRegime { get; private set; }
    public string Timeframe { get; private set; } = string.Empty;
    public string SourceExchange { get; private set; } = string.Empty;
    public string SourceLifecycleId { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public StrategyEvaluation? EntryStrategyEvaluation { get; private set; }
    public StrategyEvaluation? ExitStrategyEvaluation { get; private set; }

    private TradeJournalRecord()
    {
    }

    /// <summary>Creates a journal record at the first opening fill.</summary>
    public static TradeJournalRecord Open(
        string userId,
        Guid? strategyId,
        string strategyName,
        int? strategyVersion,
        string configurationIdentity,
        string symbol,
        TradeSide side,
        DateTime entryTimeUtc,
        decimal entryPrice,
        decimal entryQuantity,
        decimal entryFee,
        decimal? leverage,
        Guid? entryStrategyEvaluationId,
        string? entryMarketRegime,
        string timeframe,
        string sourceExchange,
        string sourceLifecycleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLifecycleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryQuantity);
        ArgumentOutOfRangeException.ThrowIfNegative(entryFee);

        return new TradeJournalRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            StrategyId = strategyId,
            StrategyName = strategyName.Trim(),
            StrategyVersion = strategyVersion,
            ConfigurationIdentity = configurationIdentity.Trim(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Side = side,
            Status = TradeLifecycleStatus.Open,
            EntryTimeUtc = DateTime.SpecifyKind(entryTimeUtc, DateTimeKind.Utc),
            EntryPrice = entryPrice,
            EntryQuantity = entryQuantity,
            Leverage = leverage,
            Fees = entryFee,
            NetPnl = -entryFee,
            EntryStrategyEvaluationId = entryStrategyEvaluationId,
            EntryMarketRegime = entryMarketRegime,
            Timeframe = timeframe.Trim(),
            SourceExchange = sourceExchange.Trim(),
            SourceLifecycleId = sourceLifecycleId.Trim(),
            ExitReason = TradeExitReason.Unknown,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Adds a scale-in fill using quantity-weighted entry pricing.</summary>
    public void AddEntryFill(DateTime filledAtUtc, decimal price, decimal quantity, decimal fee)
    {
        EnsureOpen();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(fee);

        var nextQuantity = EntryQuantity + quantity;
        EntryPrice = ((EntryPrice * EntryQuantity) + (price * quantity)) / nextQuantity;
        EntryQuantity = nextQuantity;
        EntryTimeUtc = filledAtUtc < EntryTimeUtc
            ? DateTime.SpecifyKind(filledAtUtc, DateTimeKind.Utc)
            : EntryTimeUtc;
        Fees += fee;
        RecalculateNetPnl();
    }

    /// <summary>Adds a partial or final close fill using quantity-weighted exit pricing.</summary>
    public void AddExitFill(
        DateTime filledAtUtc,
        decimal price,
        decimal quantity,
        decimal grossPnl,
        decimal fee,
        Guid? exitStrategyEvaluationId,
        TradeExitReason exitReason)
    {
        EnsureOpen();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(fee);
        if (filledAtUtc < EntryTimeUtc)
        {
            throw new InvalidOperationException("An exit fill cannot precede the trade entry.");
        }

        var nextExitQuantity = ExitQuantity + quantity;
        if (nextExitQuantity > EntryQuantity)
        {
            throw new InvalidOperationException("An exit fill cannot exceed the logical trade's remaining quantity.");
        }

        ExitPrice = (((ExitPrice ?? 0m) * ExitQuantity) + (price * quantity)) / nextExitQuantity;
        ExitQuantity = nextExitQuantity;
        GrossPnl += grossPnl;
        Fees += fee;
        ExitStrategyEvaluationId ??= exitStrategyEvaluationId;
        ExitReason = exitReason;
        RecalculateNetPnl();

        if (ExitQuantity < EntryQuantity)
        {
            Status = TradeLifecycleStatus.PartiallyClosed;
            return;
        }

        Status = TradeLifecycleStatus.Closed;
        ExitTimeUtc = DateTime.SpecifyKind(filledAtUtc, DateTimeKind.Utc);
        DurationMilliseconds = checked((long)(ExitTimeUtc.Value - EntryTimeUtc).TotalMilliseconds);
    }

    /// <summary>Stores price-excursion evidence calculated during the close transaction.</summary>
    public void SetExcursions(decimal mfeAmount, decimal mfePercent, decimal maeAmount, decimal maePercent)
    {
        if (Status != TradeLifecycleStatus.Closed)
        {
            throw new InvalidOperationException("Excursions can only be finalized for a closed trade.");
        }
        if (MfeAmount.HasValue || MfePercent.HasValue || MaeAmount.HasValue || MaePercent.HasValue)
        {
            throw new InvalidOperationException("Completed trade excursions are immutable once finalized.");
        }

        MfeAmount = mfeAmount;
        MfePercent = mfePercent;
        MaeAmount = maeAmount;
        MaePercent = maePercent;
    }

    private void EnsureOpen()
    {
        if (Status == TradeLifecycleStatus.Closed)
        {
            throw new InvalidOperationException("A completed trade journal record is immutable.");
        }
    }

    private void RecalculateNetPnl()
    {
        // Funding is currently unavailable at trade level; NetPnl therefore includes all known costs.
        NetPnl = GrossPnl - Fees + (Funding ?? 0m);
    }
}
