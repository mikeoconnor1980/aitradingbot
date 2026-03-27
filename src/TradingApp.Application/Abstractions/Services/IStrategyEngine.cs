using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Evaluates market context and determines whether a setup exists.
/// </summary>
public interface IStrategyEngine
{
    Task<StrategyEvaluation> EvaluateAsync(MarketContext context, string strategyConfigJson, CancellationToken cancellationToken = default);
}
