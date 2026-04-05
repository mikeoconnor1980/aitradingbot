namespace TradingApp.Domain.Entities;

public sealed class OptimizationResult
{
    public Guid Id { get; private set; }
    public Guid OptimizationRunId { get; private set; }
    public int Rank { get; private set; }
    public decimal FitnessScore { get; private set; }
    public string StrategyConfigJson { get; private set; } = string.Empty;
    public string SignalDescription { get; private set; } = string.Empty;
    public decimal TotalPnl { get; private set; }
    public decimal WinRate { get; private set; }
    public decimal MaxDrawdown { get; private set; }
    public int TotalTrades { get; private set; }
    public int WinningTrades { get; private set; }
    public int LosingTrades { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public decimal AverageTradePnl { get; private set; }
    public double AverageHoldTimeMinutes { get; private set; }

    private OptimizationResult()
    {
    }

    public static OptimizationResult Create(
        Guid optimizationRunId,
        int rank,
        decimal fitnessScore,
        string strategyConfigJson,
        string signalDescription,
        decimal totalPnl,
        decimal winRate,
        decimal maxDrawdown,
        int totalTrades,
        int winningTrades,
        int losingTrades,
        decimal totalFeesPaid,
        decimal averageTradePnl,
        double averageHoldTimeMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rank);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalDescription);

        return new OptimizationResult
        {
            Id = Guid.NewGuid(),
            OptimizationRunId = optimizationRunId,
            Rank = rank,
            FitnessScore = fitnessScore,
            StrategyConfigJson = strategyConfigJson,
            SignalDescription = signalDescription,
            TotalPnl = totalPnl,
            WinRate = winRate,
            MaxDrawdown = maxDrawdown,
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            TotalFeesPaid = totalFeesPaid,
            AverageTradePnl = averageTradePnl,
            AverageHoldTimeMinutes = averageHoldTimeMinutes,
        };
    }
}