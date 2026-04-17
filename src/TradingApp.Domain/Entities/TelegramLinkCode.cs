using System.Security.Cryptography;

namespace TradingApp.Domain.Entities;

public sealed class TelegramLinkCode
{
    private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(10);

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }
    public long ExpiresAtUtc { get; private set; }
    public bool IsUsed { get; private set; }

    private TelegramLinkCode()
    {
    }

    public static TelegramLinkCode Create(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TelegramLinkCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = GenerateCode(),
            CreatedAtUtc = now.ToUnixTimeMilliseconds(),
            ExpiresAtUtc = now.Add(CodeExpiry).ToUnixTimeMilliseconds(),
            IsUsed = false,
        };
    }

    public bool IsExpired(long nowUtcMs) => nowUtcMs >= ExpiresAtUtc;

    public void MarkUsed()
    {
        IsUsed = true;
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
    }
}
