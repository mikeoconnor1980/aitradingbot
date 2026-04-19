using System.Security.Cryptography;

namespace TradePilot.Domain.Entities;

public sealed class WebhookConfig
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public string? DefaultAsset { get; private set; }
    public string? TargetAgentId { get; private set; }
    public bool IsEnabled { get; private set; }
    public long CreatedAtUtc { get; private set; }
    public long UpdatedAtUtc { get; private set; }
    public long? LastTriggeredAtUtc { get; private set; }

    private WebhookConfig()
    {
    }

    public static WebhookConfig Create(Guid userId, string label, string? defaultAsset, string? targetAgentId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new WebhookConfig
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Label = label.Trim(),
            Token = GenerateToken(),
            DefaultAsset = NormalizeOptional(defaultAsset),
            TargetAgentId = NormalizeOptional(targetAgentId),
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(string label, string? defaultAsset, string? targetAgentId, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label.Trim();
        DefaultAsset = NormalizeOptional(defaultAsset);
        TargetAgentId = NormalizeOptional(targetAgentId);
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public string RegenerateToken()
    {
        Token = GenerateToken();
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Token;
    }

    public void MarkTriggered()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        LastTriggeredAtUtc = now;
        UpdatedAtUtc = now;
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}