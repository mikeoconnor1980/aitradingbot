using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IGridCycleRepository
{
    Task AddAsync(GridCycle cycle, CancellationToken cancellationToken = default);
    Task UpdateAsync(GridCycle cycle, CancellationToken cancellationToken = default);
    Task<GridCycle?> GetByGridCycleIdAsync(string gridCycleId, CancellationToken cancellationToken = default);
    Task<GridCycle?> GetActiveForStrategyAsync(string strategyName, string symbol, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GridCycle>> GetBySymbolAsync(string symbol, string? lifecycle = null, int limit = 50, CancellationToken cancellationToken = default);
}
