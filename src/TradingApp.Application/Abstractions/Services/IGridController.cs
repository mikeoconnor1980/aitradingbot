using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Manages grid lifecycle and emits trading signals.
/// </summary>
public interface IGridController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        string strategyConfigJson,
        CancellationToken cancellationToken = default);
}
