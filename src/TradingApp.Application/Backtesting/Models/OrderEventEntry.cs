namespace TradingApp.Application.Backtesting.Models;

/// <summary>
/// Order lifecycle event captured during backtest execution.
/// </summary>
public sealed record OrderEventEntry
{
    public required long TimestampUtc { get; init; }
    public required OrderEventType EventType { get; init; }
    public required string OrderId { get; init; }
    public required string Side { get; init; }
    public required string OrderType { get; init; }
    public required decimal Price { get; init; }
    public required decimal Size { get; init; }
    public decimal? FillPrice { get; init; }
    public decimal? Fee { get; init; }
    public bool? IsMaker { get; init; }
    public CancellationReason? CancellationReason { get; init; }
    public required string GridCycleId { get; init; }
}