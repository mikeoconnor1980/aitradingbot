namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestRunSummary
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required int TotalTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required DateTime CreatedAt { get; init; }
}