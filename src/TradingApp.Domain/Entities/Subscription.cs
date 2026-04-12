using TradingApp.Domain.Enums;

namespace TradingApp.Domain.Entities;

public sealed class Subscription
{
    public const int FreeTierDurationDays = 30;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SubscriptionTier Tier { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public long StartedAtUtc { get; private set; }
    public long ExpiresAtUtc { get; private set; }
    public long CreatedAtUtc { get; private set; }

    private Subscription()
    {
    }

    public static Subscription Create(Guid userId, SubscriptionTier tier, int durationDays)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(durationDays).ToUnixTimeMilliseconds();

        return new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Tier = tier,
            Status = SubscriptionStatus.Active,
            StartedAtUtc = now,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = now,
        };
    }

    public bool IsExpired(long nowUtcMs)
    {
        return nowUtcMs > ExpiresAtUtc;
    }

    public void Expire()
    {
        Status = SubscriptionStatus.Expired;
    }
}
