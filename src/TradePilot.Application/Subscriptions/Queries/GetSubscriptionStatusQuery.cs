using MediatR;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Application.Subscriptions.Queries;

public sealed record GetSubscriptionStatusQuery(Guid UserId) : Query<SubscriptionStatusResponse>;

public sealed record SubscriptionStatusResponse(
    SubscriptionTier? Tier,
    SubscriptionStatus? Status,
    long? ExpiresAtUtc,
    bool IsActive,
    string[] Features,
    string[] AllowedAssets,
    int? MaxLeverage);

public sealed class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusResponse>
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public GetSubscriptionStatusQueryHandler(
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionFeatureService subscriptionFeatureService)
    {
        _subscriptionRepository = subscriptionRepository;
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    public async Task<SubscriptionStatusResponse> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);

        if (subscription is null)
        {
            return new SubscriptionStatusResponse(null, null, null, false, [], [], null);
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
                false,
                [],
                [],
                null);
        }

        var effectiveTier = subscription.Tier == SubscriptionTier.Free
            ? SubscriptionTier.Beginner
            : subscription.Tier;
        var policy = await _subscriptionFeatureService.GetPolicyAsync(request.UserId, cancellationToken)
            ?? TierFeaturePolicy.ForTier(effectiveTier);

        return new SubscriptionStatusResponse(
            effectiveTier,
            subscription.Status,
            subscription.ExpiresAtUtc,
            true,
            [.. policy.Features.Select(feature => feature.ToString())],
            [.. policy.AllowedAssets],
            policy.MaxLeverage);
    }
}
