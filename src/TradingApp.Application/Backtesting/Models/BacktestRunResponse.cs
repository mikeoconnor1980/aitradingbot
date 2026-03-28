namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestRunResponse
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required string[] Intervals { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required GridStrategyConfig StrategyConfig { get; init; }
    public required decimal InitialCapital { get; init; }
    public required string Status { get; init; }
    public required int Progress { get; init; }
    public string? ErrorMessage { get; init; }
    public required int CandlesReplayed { get; init; }
    public required long ElapsedMs { get; init; }
    public required int TotalTrades { get; init; }
    public required int WinningTrades { get; init; }
    public required int LosingTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required decimal AverageTradePnl { get; init; }
    public required double AverageHoldTimeMinutes { get; init; }
    public required int HedgesOpened { get; init; }
    public required decimal TotalFeesPaid { get; init; }
    public required IReadOnlyList<BacktestTradeResponse> Trades { get; init; }
    public required IReadOnlyList<EquitySnapshotResponse> EquityTimeSeries { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class EquitySnapshotResponse
{
    public required long TimestampUtc { get; init; }
    public required decimal Equity { get; init; }
}