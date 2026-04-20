using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Services;

public sealed class BinanceFuturesAuthClient : IBinanceFuturesAuthClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceFuturesAuthClient> _logger;

    public BinanceFuturesAuthClient(HttpClient httpClient, ILogger<BinanceFuturesAuthClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<IReadOnlyList<BinanceBalanceSnapshot>> GetBalancesAsync(CancellationToken cancellationToken = default)
    {
        return SendReadOnlyListAsync<BinanceBalanceSnapshot>(HttpMethod.Get, "/fapi/v3/balance", null, cancellationToken);
    }

    public Task<BinanceAccountSnapshot> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<BinanceAccountSnapshot>(HttpMethod.Get, "/fapi/v2/account", null, cancellationToken);
    }

    public Task<IReadOnlyList<BinancePositionRiskSnapshot>> GetPositionRiskAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(symbol)
            ? null
            : new[] { new KeyValuePair<string, string?>("symbol", symbol) };

        return SendReadOnlyListAsync<BinancePositionRiskSnapshot>(HttpMethod.Get, "/fapi/v2/positionRisk", query, cancellationToken);
    }

    public Task<IReadOnlyList<BinanceOpenOrderSnapshot>> GetOpenOrdersAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(symbol)
            ? null
            : new[] { new KeyValuePair<string, string?>("symbol", symbol) };

        return SendReadOnlyListAsync<BinanceOpenOrderSnapshot>(HttpMethod.Get, "/fapi/v1/openOrders", query, cancellationToken);
    }

    public Task<IReadOnlyList<BinanceUserTradeSnapshot>> GetUserTradesAsync(
        string symbol,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
            new KeyValuePair<string, string?>("limit", limit.ToString(CultureInfo.InvariantCulture)),
        };

        return SendReadOnlyListAsync<BinanceUserTradeSnapshot>(HttpMethod.Get, "/fapi/v1/userTrades", query, cancellationToken);
    }

    public Task<BinancePlaceOrderResult> PlaceOrderAsync(
        BinancePlaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new List<KeyValuePair<string, string?>>
        {
            new("symbol", request.Symbol),
            new("side", request.Side),
            new("type", request.Type),
        };

        AddDecimal(query, "quantity", request.Quantity);
        AddDecimal(query, "price", request.Price);
        AddDecimal(query, "stopPrice", request.StopPrice);

        if (!string.IsNullOrWhiteSpace(request.TimeInForce))
        {
            query.Add(new("timeInForce", request.TimeInForce));
        }

        if (request.ReduceOnly)
        {
            query.Add(new("reduceOnly", "true"));
        }

        if (request.ClosePosition)
        {
            query.Add(new("closePosition", "true"));
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingType))
        {
            query.Add(new("workingType", request.WorkingType));
        }

        return SendAsync<BinancePlaceOrderResult>(HttpMethod.Post, "/fapi/v1/order", query, cancellationToken);
    }

    public Task CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default)
    {
        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
            new KeyValuePair<string, string?>("orderId", orderId.ToString(CultureInfo.InvariantCulture)),
        };

        return SendWithoutResponseAsync(HttpMethod.Delete, "/fapi/v1/order", query, cancellationToken);
    }

    public Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
        };

        return SendWithoutResponseAsync(HttpMethod.Delete, "/fapi/v1/allOpenOrders", query, cancellationToken);
    }

    public Task SetLeverageAsync(string symbol, int leverage, CancellationToken cancellationToken = default)
    {
        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
            new KeyValuePair<string, string?>("leverage", leverage.ToString(CultureInfo.InvariantCulture)),
        };

        return SendWithoutResponseAsync(HttpMethod.Post, "/fapi/v1/leverage", query, cancellationToken);
    }

    public async Task<IReadOnlyList<BinanceExchangeInfoSymbol>> GetExchangeInfoSymbolsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/fapi/v1/exchangeInfo", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("symbols", out var symbolsElement))
        {
            return [];
        }

        var result = JsonSerializer.Deserialize<List<BinanceExchangeInfoSymbol>>(symbolsElement.GetRawText(), SerializerOptions);
        return result ?? [];
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? queryParameters,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildPath(path, queryParameters));
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance authenticated API error. StatusCode={StatusCode}, Path={Path}, Body={Body}",
                (int)response.StatusCode,
                path,
                body);

            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        if (result is null)
        {
            throw new JsonException($"Unexpected Binance response for '{path}'.");
        }

        return result;
    }

    private async Task<IReadOnlyList<T>> SendReadOnlyListAsync<T>(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? queryParameters,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<List<T>>(method, path, queryParameters, cancellationToken);
        return result;
    }

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? queryParameters,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildPath(path, queryParameters));
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance authenticated API error. StatusCode={StatusCode}, Path={Path}, Body={Body}",
                (int)response.StatusCode,
                path,
                body);

            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }
    }

    private static string BuildPath(string path, IEnumerable<KeyValuePair<string, string?>>? queryParameters)
    {
        if (queryParameters is null)
        {
            return path;
        }

        var parts = queryParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}")
            .ToArray();

        return parts.Length == 0 ? path : $"{path}?{string.Join('&', parts)}";
    }

    private static void AddDecimal(ICollection<KeyValuePair<string, string?>> query, string key, decimal? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        query.Add(new KeyValuePair<string, string?>(key, value.Value.ToString(CultureInfo.InvariantCulture)));
    }

    private static void MapErrorResponse(HttpStatusCode statusCode, string body, TimeSpan? retryAfter)
    {
        var binanceCode = TryReadBinanceCode(body);

        throw statusCode switch
        {
            HttpStatusCode.Unauthorized when binanceCode == -2015 => new DomainException(
                "Binance Futures rejected the API key. Verify the key/secret pair, make sure the key is for USD-M Futures on the selected environment, enable Futures access on the key, and allow this machine's IP if the key is IP-restricted."),
            HttpStatusCode.Unauthorized => new DomainException($"Binance API key rejected: {ExtractMessage(body)}"),
            HttpStatusCode.TooManyRequests => new RateLimitException(
                $"Binance rate limit exceeded: {body}",
                retryAfter is null ? null : (int)Math.Ceiling(retryAfter.Value.TotalSeconds)),
            (HttpStatusCode)418 => new DomainException($"Binance IP banned (418). Response: {body}"),
            (HttpStatusCode)451 => new DomainException($"Binance IP banned (451). Response: {body}"),
            _ when (int)statusCode >= 400 && (int)statusCode < 500 => new DomainException(
                $"Binance API error {(int)statusCode}: {body}"),
            _ => new DomainException($"Binance API server error {(int)statusCode}: {body}"),
        };
    }

    private static int? TryReadBinanceCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var code)
                ? code
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "unknown authentication error";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("msg", out var messageElement)
                ? messageElement.GetString() ?? body
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}