using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class UserExchangeCredential
{
    private UserExchangeCredential()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Exchange Exchange { get; private set; }
    public string ApiKey { get; private set; } = string.Empty;
    public string EncryptedApiSecret { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public static UserExchangeCredential Create(Guid userId, Exchange exchange, string apiKey, string encryptedApiSecret, string label)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (exchange == Exchange.Hyperliquid)
        {
            throw new ArgumentException("UserExchangeCredential is only for key-based exchanges.", nameof(exchange));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedApiSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new UserExchangeCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Exchange = exchange,
            ApiKey = apiKey.Trim(),
            EncryptedApiSecret = encryptedApiSecret.Trim(),
            Label = label.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsActive = true,
        };
    }

    public void UpdateSecrets(string apiKey, string encryptedApiSecret, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedApiSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        ApiKey = apiKey.Trim();
        EncryptedApiSecret = encryptedApiSecret.Trim();
        Label = label.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}