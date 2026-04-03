using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Evaluates market context and determines whether a setup exists.
/// </summary>
public interface IStrategyEngine
{
    Task<StrategyEvaluation> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default);
}
