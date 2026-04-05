using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Processes signal-mode strategy evaluation results and emits trading signals
/// for position entry and exit.
/// </summary>
public interface ISignalController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default);
}