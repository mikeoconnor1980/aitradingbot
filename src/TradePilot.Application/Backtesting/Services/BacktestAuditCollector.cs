using System.Collections.Concurrent;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Backtesting.Services;

/// <summary>
/// Collects all audit log entries in-memory during a backtest run.
/// </summary>
public sealed class BacktestAuditCollector : IBacktestAuditCollector
{
    private readonly ConcurrentQueue<CandleEvaluationEntry> _candleEvaluations = new();
    private readonly ConcurrentQueue<OrderEventEntry> _orderEvents = new();
    private readonly ConcurrentQueue<GridCycleEntry> _gridCycles = new();

    public IReadOnlyList<CandleEvaluationEntry> CandleEvaluations => _candleEvaluations.ToArray();

    public IReadOnlyList<OrderEventEntry> OrderEvents => _orderEvents.ToArray();

    public IReadOnlyList<GridCycleEntry> GridCycles => _gridCycles.ToArray();

    public void LogCandleEvaluation(CandleEvaluationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _candleEvaluations.Enqueue(entry);
    }

    public void LogOrderEvent(OrderEventEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _orderEvents.Enqueue(entry);
    }

    public void LogGridCycleCompleted(GridCycleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _gridCycles.Enqueue(entry);
    }
}