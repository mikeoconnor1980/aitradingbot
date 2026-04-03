using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IStrategyRepository
{
    Task<Strategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Strategy>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
    Task<List<Strategy>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithNameAsync(string userId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task UpdateAsync(Strategy strategy, CancellationToken cancellationToken = default);
}