using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSpotCapabilities : IExchangeCapabilities
{
    private static readonly ExchangeCapabilitySet CapabilityDescriptor = new(
        Exchange.Binance,
        new HashSet<AssetType> { AssetType.Spot },
        SupportsLeverage: false,
        SupportsTriggerOrders: false,
        SupportsReduceOnly: false,
        SupportsPublicTradesStream: false,
        SupportsUserEventStream: false,
        SupportsPerUserNetworkRouting: false,
        SupportedOrderTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Market", "Limit" },
        SupportedTimeframes: new HashSet<string>(BinanceAssetMapper.ValidIntervals, StringComparer.OrdinalIgnoreCase),
        SupportsFundingRateHistory: false);

    public Exchange Exchange => Exchange.Binance;

    public ExchangeCapabilitySet CapabilitySet => CapabilityDescriptor;

    public IReadOnlySet<string> SupportedAssets => BinanceAssetMapper.SupportedAssets;

    public bool Supports(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return pair.ProductType == AssetType.Spot && SupportedAssets.Contains(pair.Base);
    }
}
