namespace TradePilot.Application.Backtesting.Models;

public sealed class BacktestDebugResponse
{
    public required string CycleId { get; init; }
    public required IReadOnlyList<CandleEvaluationEntry> CandleEvaluations { get; init; }
    public required IReadOnlyList<OrderEventEntry> OrderEvents { get; init; }
    public GridCycleEntry? GridCycleSummary { get; init; }
}