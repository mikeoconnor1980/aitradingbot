using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Services;

/// <summary>
/// Thread-safe, runtime-swappable implementation of <see cref="ISignerProvider"/>.
/// Delegates all signing operations to the inner <see cref="HyperliquidSigner"/>.
/// Allows the private key to be configured or changed at runtime via <see cref="Configure"/>.
/// </summary>
public sealed class MutableSignerProvider : ISignerProvider
{
    private readonly object _lock = new();
    private readonly ILogger<MutableSignerProvider> _logger;
    private IHyperliquidSigner? _inner;

    public MutableSignerProvider(ILogger<MutableSignerProvider> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured
    {
        get { lock (_lock) return _inner is not null; }
    }

    public string WalletAddress
    {
        get
        {
            lock (_lock)
            {
                return _inner?.WalletAddress
                    ?? throw new InvalidOperationException(
                        "No private key configured. Please configure your Hyperliquid wallet before trading.");
            }
        }
    }

    public (string R, string S, int V) SignHash(byte[] hash)
    {
        lock (_lock)
        {
            if (_inner is null)
            {
                throw new InvalidOperationException(
                    "No private key configured. Please configure your Hyperliquid wallet before signing orders.");
            }

            return _inner.SignHash(hash);
        }
    }

    public void Configure(string privateKey)
    {
        var signer = HyperliquidSigner.Create(privateKey);

        lock (_lock)
        {
            _inner = signer;
        }

        _logger.LogInformation("Wallet configured.");
        _logger.LogDebug(
            "Wallet configured: Address={WalletAddress}", signer.WalletAddress);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _inner = null;
        }

        _logger.LogInformation("Wallet configuration cleared.");
    }
}
