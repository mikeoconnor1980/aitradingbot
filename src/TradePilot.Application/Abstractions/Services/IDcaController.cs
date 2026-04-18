using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Processes DCA-mode strategy evaluation results and emits scheduled buy signals.
/// </summary>
public interface IDcaController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default);
}