using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Application.Subscriptions.Services;

public interface ISubscriptionFeatureService
{
    Task<SubscriptionTier?> GetActiveTierAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TierFeaturePolicy?> GetPolicyAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessFeatureAsync(Guid userId, Feature feature, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAllowedAssetsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int?> GetMaxLeverageAsync(Guid userId, CancellationToken cancellationToken = default);
    bool IsAssetAllowed(IReadOnlyList<string> allowedAssets, string market);
}

public sealed class SubscriptionFeatureService : ISubscriptionFeatureService
{
    private static readonly string[] QuoteSuffixes = ["USDT", "USDC", "USD"];
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionFeatureService(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<SubscriptionTier?> GetActiveTierAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (subscription.IsExpired(nowMs) || subscription.Status != SubscriptionStatus.Active)
        {
            return null;
        }

        return subscription.Tier == SubscriptionTier.Free ? SubscriptionTier.Beginner : subscription.Tier;
    }

    public async Task<TierFeaturePolicy?> GetPolicyAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tier = await GetActiveTierAsync(userId, cancellationToken);
        return tier.HasValue ? TierFeaturePolicy.ForTier(tier.Value) : null;
    }

    public async Task<bool> CanAccessFeatureAsync(Guid userId, Feature feature, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(userId, cancellationToken);
        return policy?.HasFeature(feature) == true;
    }

    public async Task<IReadOnlyList<string>> GetAllowedAssetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(userId, cancellationToken);
        return policy?.AllowedAssets ?? [];
    }

    public async Task<int?> GetMaxLeverageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(userId, cancellationToken);
        return policy?.MaxLeverage;
    }

    public bool IsAssetAllowed(IReadOnlyList<string> allowedAssets, string market)
    {
        if (allowedAssets.Count == 0)
        {
            return false;
        }

        var asset = ExtractAsset(market);
        return allowedAssets
            .Select(ExtractAsset)
            .Contains(asset, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractAsset(string market)
    {
        if (string.IsNullOrWhiteSpace(market))
        {
            return string.Empty;
        }

        var normalized = market.Trim().ToUpperInvariant();

        if (normalized.EndsWith(".P", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        if (normalized.EndsWith("-PERP", StringComparison.Ordinal))
        {
            normalized = normalized[..^5];
        }
        else if (normalized.EndsWith("PERP", StringComparison.Ordinal))
        {
            normalized = normalized[..^4];
        }

        normalized = normalized.Replace('/', '-');
        var baseSegment = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];

        foreach (var suffix in QuoteSuffixes)
        {
            if (baseSegment.EndsWith(suffix, StringComparison.Ordinal))
            {
                baseSegment = baseSegment[..^suffix.Length];
                break;
            }
        }

        return new string(baseSegment.Where(char.IsLetterOrDigit).ToArray());
    }
}