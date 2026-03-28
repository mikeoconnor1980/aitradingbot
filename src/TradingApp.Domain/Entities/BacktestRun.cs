namespace TradingApp.Domain.Entities;

public sealed class BacktestRun
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public string IntervalsJson { get; private set; } = string.Empty;
    public long StartDateUtc { get; private set; }
    public long EndDateUtc { get; private set; }
    public string StrategyConfigJson { get; private set; } = string.Empty;
    public decimal InitialCapital { get; private set; }
    public int CandlesReplayed { get; private set; }
    public long ElapsedMs { get; private set; }
    public int TotalTrades { get; private set; }
    public int WinningTrades { get; private set; }
    public int LosingTrades { get; private set; }
    public decimal WinRate { get; private set; }
    public decimal TotalPnl { get; private set; }
    public decimal MaxDrawdown { get; private set; }
    public decimal AverageTradePnl { get; private set; }
    public double AverageHoldTimeMinutes { get; private set; }
    public int HedgesOpened { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public string TradesJson { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }

    private BacktestRun()
    {
    }

    public static BacktestRun Create(
        string symbol,
        string intervalsJson,
        long startDateUtc,
        long endDateUtc,
        string strategyConfigJson,
        decimal initialCapital,
        int candlesReplayed,
        long elapsedMs,
        int totalTrades,
        int winningTrades,
        int losingTrades,
        decimal winRate,
        decimal totalPnl,
        decimal maxDrawdown,
        decimal averageTradePnl,
        double averageHoldTimeMinutes,
        int hedgesOpened,
        decimal totalFeesPaid,
        string tradesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalsJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapital);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startDateUtc, endDateUtc);

        return new BacktestRun
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            IntervalsJson = intervalsJson,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            StrategyConfigJson = strategyConfigJson,
            InitialCapital = initialCapital,
            CandlesReplayed = candlesReplayed,
            ElapsedMs = elapsedMs,
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate,
            TotalPnl = totalPnl,
            MaxDrawdown = maxDrawdown,
            AverageTradePnl = averageTradePnl,
            AverageHoldTimeMinutes = averageHoldTimeMinutes,
            HedgesOpened = hedgesOpened,
            TotalFeesPaid = totalFeesPaid,
            TradesJson = tradesJson ?? "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}