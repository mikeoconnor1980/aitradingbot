using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSymbolMapper : IExchangeSymbolMapper
{
    public Exchange Exchange => Exchange.Binance;

    public string ToExchangeSymbol(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        if (!CanMap(pair))
        {
            throw new InvalidOperationException($"Binance cannot map trading pair '{pair.Canonical}'.");
        }

        return BinanceAssetMapper.ToFuturesSymbol(pair.Base);
    }

    public TradingPair FromExchangeSymbol(string exchangeSymbol)
    {
        var normalized = BinanceAssetMapper.NormalizeSymbol(exchangeSymbol);
        if (normalized.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (normalized.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        return TradingPair.Create(normalized, "USD", AssetType.Perp);
    }

    public bool CanMap(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return BinanceAssetMapper.IsValidSymbol(pair.Base);
    }
}