using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IStrategyReviewRepository
{
    Task AddAsync(StrategyReview review, CancellationToken cancellationToken = default);

    Task<StrategyReview?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);

    Task DeleteByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);
}