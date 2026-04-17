using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Evaluates market context and determines whether a setup exists.
/// </summary>
public interface IStrategyEngine
{
    Task<StrategyEvaluation> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default);
}
