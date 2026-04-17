using System.Text.RegularExpressions;

namespace TradePilot.Domain.Entities;

public sealed partial class UserWalletAddress
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Exchange { get; private set; } = string.Empty;
    public string WalletAddress { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private UserWalletAddress()
    {
    }

    public static UserWalletAddress Create(Guid userId, string walletAddress, string exchange = "Hyperliquid")
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        if (!EthAddressRegex().IsMatch(walletAddress))
        {
            throw new ArgumentException("Invalid Ethereum address format. Must be 0x followed by 40 hex characters.", nameof(walletAddress));
        }

        return new UserWalletAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Exchange = exchange,
            WalletAddress = walletAddress,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsActive = true,
        };
    }

    public void UpdateAddress(string walletAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);

        if (!EthAddressRegex().IsMatch(walletAddress))
        {
            throw new ArgumentException("Invalid Ethereum address format. Must be 0x followed by 40 hex characters.", nameof(walletAddress));
        }

        WalletAddress = walletAddress;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    [GeneratedRegex(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled)]
    private static partial Regex EthAddressRegex();
}
