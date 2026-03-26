using Nethereum.ABI.EIP712;

namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidSigner
{
    string WalletAddress { get; }

    (string R, string S, int V) SignTypedData<TDomain>(TypedData<TDomain> typedData) where TDomain : IDomain;

    (string R, string S, int V) SignHash(byte[] hash);
}
