namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestTradeResponse
{
    public required DateTime EntryTime { get; init; }
    public required DateTime? ExitTime { get; init; }
    public required decimal EntryPrice { get; init; }
    public required decimal? ExitPrice { get; init; }
    public required string Side { get; init; }
    public required decimal Size { get; init; }
    public required decimal? Pnl { get; init; }
    public required decimal Fees { get; init; }
    public required string TradeType { get; init; }
    public required string GridCycleId { get; init; }
    public string? ExitReason { get; init; }
}