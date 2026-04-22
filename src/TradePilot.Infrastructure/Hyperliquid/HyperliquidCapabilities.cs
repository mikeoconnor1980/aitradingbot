using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidCapabilities : IExchangeCapabilities
{
    private static readonly ExchangeCapabilitySet CapabilityDescriptor = new(
        Exchange.Hyperliquid,
        new HashSet<AssetType> { AssetType.Perp },
        SupportsLeverage: true,
        SupportsTriggerOrders: true,
        SupportsReduceOnly: true,
        SupportsPublicTradesStream: true,
        SupportsUserEventStream: true,
        SupportsPerUserNetworkRouting: true,
        SupportedOrderTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Market", "Limit", "Trigger" },
        SupportedTimeframes: new HashSet<string>(HyperliquidAssetMapper.GetSupportedTimeframes(), StringComparer.Ordinal),
        SupportsFundingRateHistory: false);

    public Exchange Exchange => Exchange.Hyperliquid;

    public ExchangeCapabilitySet CapabilitySet => CapabilityDescriptor;

    public bool Supports(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return pair.ProductType == AssetType.Perp;
    }
}