using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
