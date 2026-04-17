namespace TradePilot.Application.Abstractions.Services;

public interface IHyperliquidSigner
{
    string WalletAddress { get; }

    (string R, string S, int V) SignHash(byte[] hash);
}
