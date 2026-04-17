using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface ILiveOrderRepository
{
    Task AddAsync(LiveOrder order, CancellationToken cancellationToken = default);
    Task UpdateAsync(LiveOrder order, CancellationToken cancellationToken = default);
    Task<LiveOrder?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveOrder>> GetByGridCycleIdAsync(string gridCycleId, CancellationToken cancellationToken = default);
}
