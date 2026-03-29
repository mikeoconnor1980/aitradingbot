namespace TradingApp.Application.Backtesting.Models;

/// <summary>
/// Summary of a completed grid cycle for audit purposes.
/// </summary>
public sealed record GridCycleEntry
{
    public required string GridCycleId { get; init; }
    public required long DeployTimestampUtc { get; init; }
    public required decimal AnchorPrice { get; init; }
    public required int LevelsPlaced { get; init; }
    public required IReadOnlyList<decimal> LevelPrices { get; init; }
    public required int LevelsFilled { get; init; }
    public required decimal TakeProfitPrice { get; init; }
    public required decimal? StopLossPrice { get; init; }
    public required string ExitReason { get; init; }
    public required decimal CyclePnl { get; init; }
    public required long CycleDurationMs { get; init; }
    public required long CloseTimestampUtc { get; init; }
}