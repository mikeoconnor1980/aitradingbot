using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Processes signal-mode strategy evaluation results and emits trading signals
/// for position entry and exit.
/// </summary>
public interface ISignalController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluationResult evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default);
}
