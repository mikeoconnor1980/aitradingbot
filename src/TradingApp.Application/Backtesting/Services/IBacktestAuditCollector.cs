using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Collects audit/debug data during a backtest run.
/// Implementations: BacktestAuditCollector (active) and NullBacktestAuditCollector (disabled/live).
/// </summary>
public interface IBacktestAuditCollector
{
    void LogCandleEvaluation(CandleEvaluationEntry entry);

    void LogOrderEvent(OrderEventEntry entry);

    void LogGridCycleCompleted(GridCycleEntry entry);
}