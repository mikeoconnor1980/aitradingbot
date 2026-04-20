namespace TradePilot.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public long CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public string PreferredNetwork { get; private set; } = "mainnet";
    public string PreferredExchange { get; private set; } = "Hyperliquid";
    public string? AuthProvider { get; private set; }
    public string? ExternalProviderId { get; private set; }
    public long? TelegramChatId { get; private set; }

    private User()
    {
    }

    public static User Create(string email, string displayName, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsActive = true,
            PreferredNetwork = "mainnet",
            PreferredExchange = "Hyperliquid",
        };
    }

    public static User CreateExternal(string email, string displayName, string authProvider, string externalProviderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderId);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = null,
            AuthProvider = authProvider,
            ExternalProviderId = externalProviderId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsActive = true,
            PreferredNetwork = "mainnet",
            PreferredExchange = "Hyperliquid",
        };
    }

    public void LinkExternalProvider(string authProvider, string externalProviderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProviderId);
        AuthProvider = authProvider;
        ExternalProviderId = externalProviderId;
    }

    public void UpdateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void UpdatePreferredNetwork(string network)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);

        if (network is not ("mainnet" or "testnet"))
        {
            throw new ArgumentException("Network must be 'mainnet' or 'testnet'.", nameof(network));
        }

        PreferredNetwork = network;
    }

    public void UpdatePreferredExchange(string exchange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        if (exchange is not ("Hyperliquid" or "Binance"))
        {
            throw new ArgumentException("Exchange must be 'Hyperliquid' or 'Binance'.", nameof(exchange));
        }

        PreferredExchange = exchange;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void LinkTelegram(long chatId)
    {
        TelegramChatId = chatId;
    }

    public void UnlinkTelegram()
    {
        TelegramChatId = null;
    }
}
