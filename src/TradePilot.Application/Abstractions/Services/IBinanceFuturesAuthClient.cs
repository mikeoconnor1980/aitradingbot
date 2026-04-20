using System.Text.Json.Serialization;

namespace TradePilot.Application.Abstractions.Services;

public interface IBinanceFuturesAuthClient
{
    Task<IReadOnlyList<BinanceBalanceSnapshot>> GetBalancesAsync(CancellationToken cancellationToken = default);

    Task<BinanceAccountSnapshot> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinancePositionRiskSnapshot>> GetPositionRiskAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinanceOpenOrderSnapshot>> GetOpenOrdersAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinanceUserTradeSnapshot>> GetUserTradesAsync(
        string symbol,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<BinancePlaceOrderResult> PlaceOrderAsync(
        BinancePlaceOrderRequest request,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default);

    Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default);

    Task SetLeverageAsync(string symbol, int leverage, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinanceExchangeInfoSymbol>> GetExchangeInfoSymbolsAsync(CancellationToken cancellationToken = default);
}

public sealed class BinancePlaceOrderRequest
{
    public string Symbol { get; init; } = string.Empty;

    public string Side { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public decimal? Quantity { get; init; }

    public decimal? Price { get; init; }

    public decimal? StopPrice { get; init; }

    public string? TimeInForce { get; init; }

    public bool ReduceOnly { get; init; }

    public bool ClosePosition { get; init; }

    public string? WorkingType { get; init; }
}

public sealed class BinancePlaceOrderResult
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed class BinanceExchangeInfoSymbol
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("baseAsset")]
    public string BaseAsset { get; init; } = string.Empty;

    [JsonPropertyName("quoteAsset")]
    public string QuoteAsset { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("filters")]
    public IReadOnlyList<BinanceExchangeFilter> Filters { get; init; } = [];
}

public sealed class BinanceExchangeFilter
{
    [JsonPropertyName("filterType")]
    public string FilterType { get; init; } = string.Empty;

    [JsonPropertyName("tickSize")]
    public string TickSize { get; init; } = string.Empty;

    [JsonPropertyName("stepSize")]
    public string StepSize { get; init; } = string.Empty;
}

public sealed class BinanceBalanceSnapshot
{
    [JsonPropertyName("asset")]
    public string Asset { get; init; } = string.Empty;

    [JsonPropertyName("balance")]
    public string Balance { get; init; } = string.Empty;

    [JsonPropertyName("crossWalletBalance")]
    public string CrossWalletBalance { get; init; } = string.Empty;

    [JsonPropertyName("crossUnPnl")]
    public string CrossUnrealizedPnl { get; init; } = string.Empty;

    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; init; } = string.Empty;
}

public sealed class BinanceAccountSnapshot
{
    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; init; } = string.Empty;

    [JsonPropertyName("totalWalletBalance")]
    public string TotalWalletBalance { get; init; } = string.Empty;

    [JsonPropertyName("totalCrossWalletBalance")]
    public string TotalCrossWalletBalance { get; init; } = string.Empty;

    [JsonPropertyName("totalUnrealizedProfit")]
    public string TotalUnrealizedProfit { get; init; } = string.Empty;

    [JsonPropertyName("totalCrossUnPnl")]
    public string TotalCrossUnrealizedPnl { get; init; } = string.Empty;

    [JsonPropertyName("totalMaintMargin")]
    public string TotalMaintenanceMargin { get; init; } = string.Empty;
}

public sealed class BinancePositionRiskSnapshot
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("positionAmt")]
    public string PositionAmount { get; init; } = string.Empty;

    [JsonPropertyName("entryPrice")]
    public string EntryPrice { get; init; } = string.Empty;

    [JsonPropertyName("markPrice")]
    public string MarkPrice { get; init; } = string.Empty;

    [JsonPropertyName("unRealizedProfit")]
    public string UnrealizedProfit { get; init; } = string.Empty;

    [JsonPropertyName("liquidationPrice")]
    public string LiquidationPrice { get; init; } = string.Empty;

    [JsonPropertyName("leverage")]
    public string Leverage { get; init; } = string.Empty;

    [JsonPropertyName("marginType")]
    public string MarginType { get; init; } = string.Empty;

    [JsonPropertyName("isolatedMargin")]
    public string IsolatedMargin { get; init; } = string.Empty;
}

public sealed class BinanceOpenOrderSnapshot
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = string.Empty;

    [JsonPropertyName("origQty")]
    public string OriginalQuantity { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("stopPrice")]
    public string StopPrice { get; init; } = string.Empty;

    [JsonPropertyName("reduceOnly")]
    public bool ReduceOnly { get; init; }

    [JsonPropertyName("closePosition")]
    public bool ClosePosition { get; init; }
}

public sealed class BinanceUserTradeSnapshot
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("price")]
    public string Price { get; init; } = string.Empty;

    [JsonPropertyName("qty")]
    public string Quantity { get; init; } = string.Empty;

    [JsonPropertyName("commission")]
    public string Commission { get; init; } = string.Empty;

    [JsonPropertyName("realizedPnl")]
    public string RealizedPnl { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("buyer")]
    public bool Buyer { get; init; }
}