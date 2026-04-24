using System.Text.Json.Serialization;

namespace TradePilot.Application.Abstractions.Services;

public interface IBinanceSpotAuthClient
{
    Task<BinanceSpotAccountInfo> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinanceSpotOpenOrder>> GetOpenOrdersAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default);

    Task<BinanceSpotOrderResult> PlaceOrderAsync(
        BinanceSpotPlaceOrderRequest request,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default);

    Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BinanceSpotUserTrade>> GetUserTradesAsync(
        string symbol,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed class BinanceSpotPlaceOrderRequest
{
    public string Symbol { get; init; } = string.Empty;

    public string Side { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public decimal? Quantity { get; init; }

    public decimal? QuoteOrderQty { get; init; }

    public decimal? Price { get; init; }

    public string? TimeInForce { get; init; }

    public string? NewOrderRespType { get; init; }
}

public sealed class BinanceSpotOrderResult
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; init; } = string.Empty;

    [JsonPropertyName("cummulativeQuoteQty")]
    public string CummulativeQuoteQty { get; init; } = string.Empty;
}

public sealed class BinanceSpotAccountInfo
{
    [JsonPropertyName("balances")]
    public IReadOnlyList<BinanceSpotBalance> Balances { get; init; } = [];
}

public sealed class BinanceSpotBalance
{
    [JsonPropertyName("asset")]
    public string Asset { get; init; } = string.Empty;

    [JsonPropertyName("free")]
    public string Free { get; init; } = string.Empty;

    [JsonPropertyName("locked")]
    public string Locked { get; init; } = string.Empty;
}

public sealed class BinanceSpotOpenOrder
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = string.Empty;

    [JsonPropertyName("origQty")]
    public string OrigQty { get; init; } = string.Empty;

    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }
}

public sealed class BinanceSpotUserTrade
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = string.Empty;

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = string.Empty;

    [JsonPropertyName("quoteQty")]
    public string QuoteQty { get; init; } = string.Empty;

    [JsonPropertyName("commission")]
    public string Commission { get; init; } = string.Empty;

    [JsonPropertyName("commissionAsset")]
    public string CommissionAsset { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("isBuyer")]
    public bool IsBuyer { get; init; }
}
