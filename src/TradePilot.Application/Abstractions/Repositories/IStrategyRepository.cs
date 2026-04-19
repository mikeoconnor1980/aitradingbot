using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IStrategyRepository
{
    Task<Strategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Strategy?> GetRunningAssignedToAgentAsync(string agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Strategy>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
    Task<List<Strategy>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithNameAsync(string userId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> SearchIdsByNameAsync(string nameContains, CancellationToken cancellationToken = default);
    Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task UpdateAsync(Strategy strategy, CancellationToken cancellationToken = default);
}