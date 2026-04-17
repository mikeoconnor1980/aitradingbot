namespace TradePilot.Application.Optimization.Models;

public sealed record OutOfSampleMetrics
{
    public required decimal TotalPnl { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required int TotalTrades { get; init; }
    public required decimal FitnessScore { get; init; }
}
