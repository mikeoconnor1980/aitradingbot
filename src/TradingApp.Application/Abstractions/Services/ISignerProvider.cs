namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Extends <see cref="IHyperliquidSigner"/> with runtime key management.
/// Allows the private key to be configured or swapped without restarting the application.
/// Delegates all signing operations to the currently-loaded inner signer.
/// </summary>
public interface ISignerProvider : IHyperliquidSigner
{
    bool IsConfigured { get; }
    void Configure(string privateKey);
    void Clear();
}
