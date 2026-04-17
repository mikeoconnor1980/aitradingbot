namespace TradePilot.Domain.Entities;

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
    public decimal? OosTotalPnl { get; private set; }
    public decimal? OosWinRate { get; private set; }
    public decimal? OosMaxDrawdown { get; private set; }
    public int? OosTotalTrades { get; private set; }
    public decimal? OosFitnessScore { get; private set; }
    public decimal? SharpeRatio { get; private set; }
    public decimal? SortinoRatio { get; private set; }
    public decimal? ProfitFactor { get; private set; }
    public decimal? CalmarRatio { get; private set; }

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
        double averageHoldTimeMinutes,
        decimal? oosTotalPnl = null,
        decimal? oosWinRate = null,
        decimal? oosMaxDrawdown = null,
        int? oosTotalTrades = null,
        decimal? oosFitnessScore = null,
        decimal? sharpeRatio = null,
        decimal? sortinoRatio = null,
        decimal? profitFactor = null,
        decimal? calmarRatio = null)
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
            OosTotalPnl = oosTotalPnl,
            OosWinRate = oosWinRate,
            OosMaxDrawdown = oosMaxDrawdown,
            OosTotalTrades = oosTotalTrades,
            OosFitnessScore = oosFitnessScore,
            SharpeRatio = sharpeRatio,
            SortinoRatio = sortinoRatio,
            ProfitFactor = profitFactor,
            CalmarRatio = calmarRatio,
        };
    }
}