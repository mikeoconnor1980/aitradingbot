using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public sealed class StrategyTierConstraintValidator
{
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;

    public StrategyTierConstraintValidator(ISubscriptionFeatureService subscriptionFeatureService)
    {
        _subscriptionFeatureService = subscriptionFeatureService;
    }

    public async Task ValidateAsync(
        AppIdentity identity,
        StrategyConfig config,
        bool templateIsBeginnerVisible,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(config);

        if (!Guid.TryParse(identity.UserId, out var userId))
        {
            return;
        }

        var policy = await _subscriptionFeatureService.GetPolicyAsync(userId, cancellationToken);
        if (policy is null)
        {
            throw new DomainException("An active subscription is required to use trading features.");
        }

        if (!_subscriptionFeatureService.IsAssetAllowed(policy.AllowedAssets, config.Market))
        {
            throw new DomainException($"Your current tier only supports {string.Join(", ", policy.AllowedAssets)} markets.");
        }

        if (policy.MaxLeverage.HasValue && config.Risk.Leverage > policy.MaxLeverage.Value)
        {
            throw new DomainException($"Your current tier supports a maximum of {policy.MaxLeverage.Value}x leverage.");
        }

        if (!policy.HasFeature(Feature.FullStrategyLibrary) && !templateIsBeginnerVisible)
        {
            throw new DomainException("This strategy template is only available on the Pro tier.");
        }
    }
}