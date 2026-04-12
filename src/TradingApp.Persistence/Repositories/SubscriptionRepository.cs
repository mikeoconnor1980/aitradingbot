using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly TradingAppDbContext _db;

    public SubscriptionRepository(TradingAppDbContext db)
    {
        _db = db;
    }

    public async Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await _db.Subscriptions.AddAsync(subscription, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
