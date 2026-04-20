using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceCapabilities : IExchangeCapabilities
{
    private static readonly HashSet<string> SupportedAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTC",
        "ETH",
    };

    private static readonly ExchangeCapabilitySet CapabilityDescriptor = new(
        Exchange.Binance,
        new HashSet<AssetType> { AssetType.Perp },
        SupportsLeverage: true,
        SupportsTriggerOrders: true,
        SupportsReduceOnly: true,
        SupportsPublicTradesStream: false,
        SupportsUserEventStream: false,
        SupportsPerUserNetworkRouting: false,
        SupportedOrderTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Market", "Limit", "Trigger" },
        SupportedTimeframes: new HashSet<string>(BinanceAssetMapper.ValidIntervals, StringComparer.OrdinalIgnoreCase));

    public Exchange Exchange => Exchange.Binance;

    public ExchangeCapabilitySet CapabilitySet => CapabilityDescriptor;

    public bool Supports(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return pair.ProductType == AssetType.Perp && SupportedAssets.Contains(pair.Base);
    }
}