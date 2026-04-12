using MediatR;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Subscriptions.Queries;

public sealed record GetSubscriptionStatusQuery(Guid UserId) : Query<SubscriptionStatusResponse>;

public sealed record SubscriptionStatusResponse(
    SubscriptionTier? Tier,
    SubscriptionStatus? Status,
    long? ExpiresAtUtc,
    bool IsActive);

public sealed class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusResponse>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GetSubscriptionStatusQueryHandler(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<SubscriptionStatusResponse> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);

        if (subscription is null)
        {
            return new SubscriptionStatusResponse(null, null, null, false);
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (subscription.IsExpired(nowMs))
        {
            subscription.Expire();
            await _subscriptionRepository.SaveChangesAsync(cancellationToken);

            return new SubscriptionStatusResponse(
                subscription.Tier,
                SubscriptionStatus.Expired,
                subscription.ExpiresAtUtc,
                false);
        }

        return new SubscriptionStatusResponse(
            subscription.Tier,
            subscription.Status,
            subscription.ExpiresAtUtc,
            true);
    }
}
