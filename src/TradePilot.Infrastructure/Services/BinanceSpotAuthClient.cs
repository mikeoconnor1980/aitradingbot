using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Services;

public sealed class BinanceSpotAuthClient : IBinanceSpotAuthClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceSpotAuthClient> _logger;

    public BinanceSpotAuthClient(HttpClient httpClient, ILogger<BinanceSpotAuthClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<BinanceSpotAccountInfo> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<BinanceSpotAccountInfo>(HttpMethod.Get, "/api/v3/account", null, cancellationToken);
    }

    public Task<IReadOnlyList<BinanceSpotOpenOrder>> GetOpenOrdersAsync(
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(symbol)
            ? null
            : new[] { new KeyValuePair<string, string?>("symbol", symbol) };

        return SendReadOnlyListAsync<BinanceSpotOpenOrder>(HttpMethod.Get, "/api/v3/openOrders", query, cancellationToken);
    }

    public Task<BinanceSpotOrderResult> PlaceOrderAsync(
        BinanceSpotPlaceOrderRequest request,
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
        AddDecimal(query, "quoteOrderQty", request.QuoteOrderQty);
        AddDecimal(query, "price", request.Price);

        if (!string.IsNullOrWhiteSpace(request.TimeInForce))
        {
            query.Add(new("timeInForce", request.TimeInForce));
        }

        if (!string.IsNullOrWhiteSpace(request.NewOrderRespType))
        {
            query.Add(new("newOrderRespType", request.NewOrderRespType));
        }

        return SendAsync<BinanceSpotOrderResult>(HttpMethod.Post, "/api/v3/order", query, cancellationToken);
    }

    public Task CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default)
    {
        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
            new KeyValuePair<string, string?>("orderId", orderId.ToString(CultureInfo.InvariantCulture)),
        };

        return SendWithoutResponseAsync(HttpMethod.Delete, "/api/v3/order", query, cancellationToken);
    }

    public Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var query = new[]
        {
            new KeyValuePair<string, string?>("symbol", symbol),
        };

        return SendWithoutResponseAsync(HttpMethod.Delete, "/api/v3/openOrders", query, cancellationToken);
    }

    public Task<IReadOnlyList<BinanceSpotUserTrade>> GetUserTradesAsync(
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

        return SendReadOnlyListAsync<BinanceSpotUserTrade>(HttpMethod.Get, "/api/v3/myTrades", query, cancellationToken);
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
                "Binance Spot API error. StatusCode={StatusCode}, Path={Path}, Body={Body}",
                (int)response.StatusCode,
                path,
                body);

            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        if (result is null)
        {
            throw new JsonException($"Unexpected Binance Spot response for '{path}'.");
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
                "Binance Spot API error. StatusCode={StatusCode}, Path={Path}, Body={Body}",
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
        var message = ExtractMessage(body);

        throw statusCode switch
        {
            HttpStatusCode.Unauthorized when binanceCode == -2015 => new BinanceApiException(
                BuildMessage(
                    "Binance Spot rejected the API key. Verify the key/secret pair, enable Spot access on the key, and allow this machine's IP if the key is IP-restricted.",
                    binanceCode,
                    message),
                (int)statusCode,
                binanceCode,
                isTransient: false),
            HttpStatusCode.Unauthorized => new BinanceApiException(
                BuildMessage("Binance Spot authentication failed", binanceCode, message),
                (int)statusCode,
                binanceCode,
                isTransient: false),
            HttpStatusCode.Forbidden => new BinanceApiException(
                BuildMessage(
                    "Binance Spot API access forbidden. The API key may be disabled, restricted, or this IP may not be whitelisted.",
                    binanceCode,
                    message),
                (int)statusCode,
                binanceCode,
                isTransient: false),
            HttpStatusCode.TooManyRequests => new RateLimitException(
                BuildMessage("Binance Spot rate limit exceeded", binanceCode, message),
                (int)statusCode,
                retryAfter is null ? null : (int)Math.Ceiling(retryAfter.Value.TotalSeconds)),
            (HttpStatusCode)418 => new RateLimitException(
                BuildMessage("Binance Spot IP ban triggered", binanceCode, message),
                (int)statusCode,
                retryAfter is null ? null : (int)Math.Ceiling(retryAfter.Value.TotalSeconds)),
            _ when binanceCode == -1111 => new DomainException(
                BuildMessage("Invalid order quantity", binanceCode, message)),
            _ when binanceCode == -2010 => new DomainException(
                BuildMessage("Insufficient balance", binanceCode, message)),
            _ when (int)statusCode >= 400 && (int)statusCode < 500 => new DomainException(
                BuildMessage($"Binance Spot API error {(int)statusCode}", binanceCode, message)),
            _ when (int)statusCode >= 500 => new BinanceApiException(
                BuildMessage($"Binance Spot server error {(int)statusCode}", binanceCode, message),
                (int)statusCode,
                binanceCode,
                isTransient: true),
            _ => new DomainException(BuildMessage($"Unexpected Binance Spot response {(int)statusCode}", binanceCode, message)),
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
            return "unknown error";
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

    private static string BuildMessage(string summary, int? binanceCode, string message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "no additional Binance error details were provided"
            : message;

        return binanceCode is int code
            ? $"{summary} (Binance {code}): {normalizedMessage}"
            : $"{summary}: {normalizedMessage}";
    }
}
