using TradePilot.Application.Abstractions.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IStrategyRevisionRepository
{
    Task AddAsync(StrategyRevision revision, CancellationToken cancellationToken = default);

    Task<StrategyRevision?> GetByStrategyAndRevisionAsync(
        Guid strategyId,
        int revisionNumber,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StrategyRevision>> GetPagedByStrategyIdAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestRevisionNumberAsync(Guid strategyId, CancellationToken cancellationToken = default);
}