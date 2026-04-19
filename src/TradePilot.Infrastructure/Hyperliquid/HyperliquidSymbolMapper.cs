using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidSymbolMapper : IExchangeSymbolMapper
{
    public Exchange Exchange => Exchange.Hyperliquid;

    public string ToExchangeSymbol(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        if (!CanMap(pair))
        {
            throw new InvalidOperationException($"Hyperliquid cannot map trading pair '{pair.Canonical}'.");
        }

        return pair.Base;
    }

    public TradingPair FromExchangeSymbol(string exchangeSymbol)
    {
        var coin = HyperliquidAssetMapper.ToCoin(exchangeSymbol);
        return TradingPair.Create(coin, "USD", AssetType.Perp);
    }

    public bool CanMap(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return pair.ProductType == AssetType.Perp && !string.IsNullOrWhiteSpace(pair.Base);
    }
}