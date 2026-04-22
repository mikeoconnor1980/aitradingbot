namespace TradePilot.Application.Abstractions.Services;

public sealed record ExchangeCapabilitySet(
    Exchange Exchange,
    IReadOnlySet<AssetType> SupportedProductTypes,
    bool SupportsLeverage,
    bool SupportsTriggerOrders,
    bool SupportsReduceOnly,
    bool SupportsPublicTradesStream,
    bool SupportsUserEventStream,
    bool SupportsPerUserNetworkRouting,
    IReadOnlySet<string> SupportedOrderTypes,
    IReadOnlySet<string> SupportedTimeframes,
    bool SupportsFundingRateHistory = false);