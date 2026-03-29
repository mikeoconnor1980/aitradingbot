using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// No-op audit collector used when audit logging is disabled or in live trading.
/// </summary>
public sealed class NullBacktestAuditCollector : IBacktestAuditCollector
{
    public static readonly NullBacktestAuditCollector Instance = new();

    private NullBacktestAuditCollector()
    {
    }

    public void LogCandleEvaluation(CandleEvaluationEntry entry)
    {
    }

    public void LogOrderEvent(OrderEventEntry entry)
    {
    }

    public void LogGridCycleCompleted(GridCycleEntry entry)
    {
    }
}