namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestResult
{
    public required int TotalTrades { get; init; }
    public required int WinningTrades { get; init; }
    public required int LosingTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnL { get; init; }
    public required decimal MaxDrawdownAbsolute { get; init; }
    public required decimal MaxDrawdownPercent { get; init; }
    public required decimal AverageTradePnL { get; init; }
    public required TimeSpan AverageHoldTime { get; init; }
    public required int HedgesOpened { get; init; }
    public required decimal TotalFeesPaid { get; init; }
    public required int GridCycles { get; init; }
    public required int CandlesReplayed { get; init; }
    public required decimal FinalEquity { get; init; }
    public required IReadOnlyList<EquitySnapshot> EquityTimeSeries { get; init; }
    public required IReadOnlyList<BacktestTrade> TradeLog { get; init; }
    public IReadOnlyList<CandleEvaluationEntry>? CandleEvaluationLog { get; init; }
    public IReadOnlyList<OrderEventEntry>? OrderEventLog { get; init; }
    public IReadOnlyList<GridCycleEntry>? GridCycleLog { get; init; }
}
