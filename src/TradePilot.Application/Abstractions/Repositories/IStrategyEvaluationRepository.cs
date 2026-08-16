using TradePilot.Application.StrategyEvaluations.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

/// <summary>Stores and queries immutable deterministic strategy-evaluation evidence.</summary>
public interface IStrategyEvaluationRepository
{
    Task AddAsync(StrategyEvaluation evaluation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrategyEvaluation>> GetAsync(
        StrategyEvaluationFilter filter,
        int limit,
        CancellationToken cancellationToken = default);

    Task<StrategyEvaluation?> GetLatestAsync(
        StrategyEvaluationFilter filter,
        CancellationToken cancellationToken = default);

    Task<StrategyEvaluationSummary> GetSummaryAsync(
        StrategyEvaluationFilter filter,
        CancellationToken cancellationToken = default);
}
