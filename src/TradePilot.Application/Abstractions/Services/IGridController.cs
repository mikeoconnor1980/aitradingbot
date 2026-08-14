using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Manages grid lifecycle and emits trading signals.
/// </summary>
public interface IGridController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluationResult evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default);
}
