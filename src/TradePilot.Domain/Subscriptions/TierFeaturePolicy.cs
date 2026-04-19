using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Subscriptions;

public sealed class TierFeaturePolicy
{
    private TierFeaturePolicy(
        SubscriptionTier tier,
        IReadOnlySet<Feature> features,
        IReadOnlyList<string> allowedAssets,
        int? maxLeverage)
    {
        Tier = tier;
        Features = features;
        AllowedAssets = allowedAssets;
        MaxLeverage = maxLeverage;
    }

    public SubscriptionTier Tier { get; }
    public IReadOnlySet<Feature> Features { get; }
    public IReadOnlyList<string> AllowedAssets { get; }
    public int? MaxLeverage { get; }

    public bool HasFeature(Feature feature)
    {
        return Features.Contains(feature);
    }

    public static TierFeaturePolicy ForTier(SubscriptionTier tier)
    {
        return tier switch
        {
            SubscriptionTier.Pro => new TierFeaturePolicy(
                SubscriptionTier.Pro,
                new HashSet<Feature>
                {
                    Feature.MacroCalendar,
                    Feature.AiReview,
                    Feature.Optimizer,
                    Feature.FullStrategyLibrary,
                    Feature.AllAssets,
                    Feature.UnrestrictedLeverage,
                    Feature.Webhooks,
                },
                ["BTC", "ETH", "SOL", "DOGE", "AVAX", "ARB", "LINK", "OP"],
                null),
            _ => new TierFeaturePolicy(
                SubscriptionTier.Beginner,
                new HashSet<Feature>(),
                ["BTC", "ETH"],
                5),
        };
    }
}