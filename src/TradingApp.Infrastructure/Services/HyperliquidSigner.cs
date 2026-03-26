using Nethereum.ABI.EIP712;
using Nethereum.Signer;
using Nethereum.Util;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidSigner : IHyperliquidSigner
{
    private readonly EthECKey _ecKey;

    public string WalletAddress { get; }

    private HyperliquidSigner(EthECKey ecKey, string walletAddress)
    {
        _ecKey = ecKey;
        WalletAddress = walletAddress;
    }

    public static HyperliquidSigner Create(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException(
                "Hyperliquid private key is missing. Set 'Hyperliquid__PrivateKey' environment variable or add 'Hyperliquid:PrivateKey' to appsettings.Development.json.",
                nameof(privateKey));
        }

        var normalised = privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? privateKey[2..]
            : privateKey;

        if (normalised.Length != 64 || !IsHex(normalised))
        {
            throw new ArgumentException(
                "Hyperliquid private key is malformed. Expected a 64-character hex string (with optional '0x' prefix).",
                nameof(privateKey));
        }

        try
        {
            var ecKey = new EthECKey(privateKey);
            var address = ecKey.GetPublicAddress();
            return new HyperliquidSigner(ecKey, address);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to derive wallet address from private key: {ex.Message}. Ensure the key is a valid Ethereum-compatible private key.",
                nameof(privateKey),
                ex);
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    public (string R, string S, int V) SignTypedData<TDomain>(TypedData<TDomain> typedData) where TDomain : IDomain
    {
        var encoder = new Eip712TypedDataEncoder();
        var hash = encoder.EncodeAndHashTypedData(typedData);
        return SignHash(hash);
    }

    public (string R, string S, int V) SignHash(byte[] hash)
    {
        var signature = _ecKey.SignAndCalculateV(hash);

        var r = "0x" + Convert.ToHexString(signature.R).ToLowerInvariant();
        var s = "0x" + Convert.ToHexString(signature.S).ToLowerInvariant();
        var v = signature.V.Length > 0
            ? signature.V[0]
            : throw new InvalidOperationException("EIP-712 signature produced an empty V component.");

        return (r, s, v);
    }
}
