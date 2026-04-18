using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IStrategyTemplateRepository
{
    Task<StrategyTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StrategyTemplate?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StrategyTemplate>> GetActiveOrderedAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithSlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default);
    Task AddAsync(StrategyTemplate template, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
