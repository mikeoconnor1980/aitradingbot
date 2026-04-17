using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Infrastructure.Binance.Models;

namespace TradePilot.Infrastructure.Services;

public sealed class BinanceFuturesRestClient : IBinanceFuturesRestClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceFuturesRestClient> _logger;

    public BinanceFuturesRestClient(HttpClient httpClient, ILogger<BinanceFuturesRestClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CandleSnapshotDto>> GetKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var url = BuildKlinesUrl("/fapi/v1/klines", futuresSymbol, interval, startTime, endTime, limit);
        var klines = await GetCandlesAsync(url, cancellationToken);

        _logger.LogDebug(
            "Binance klines fetched. Symbol={Symbol}, Interval={Interval}, StartTime={StartTime}, EndTime={EndTime}, Count={Count}",
            futuresSymbol,
            interval,
            startTime,
            endTime,
            klines.Count);

        return klines;
    }

    public async Task<IReadOnlyList<CandleSnapshotDto>> GetMarkPriceKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime = null,
        int limit = 1500,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var url = BuildKlinesUrl("/fapi/v1/markPriceKlines", futuresSymbol, interval, startTime, endTime, limit);
        var klines = await GetCandlesAsync(url, cancellationToken);

        _logger.LogDebug(
            "Binance mark price klines fetched. Symbol={Symbol}, Interval={Interval}, StartTime={StartTime}, EndTime={EndTime}, Count={Count}",
            futuresSymbol,
            interval,
            startTime,
            endTime,
            klines.Count);

        return klines;
    }

    public async Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
        string futuresSymbol,
        long startTime,
        long? endTime = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(futuresSymbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var url = BuildFundingRatesUrl(futuresSymbol, startTime, endTime, limit);
        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance API error. StatusCode={StatusCode}, Endpoint={Endpoint}, Body={Body}",
                (int)response.StatusCode,
                url,
                body);

            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rates = await JsonSerializer.DeserializeAsync<List<BinanceFundingRate>>(stream, cancellationToken: cancellationToken);
        if (rates is null)
        {
            throw new JsonException("Unexpected Binance funding rate response shape — expected a JSON array.");
        }

        var result = rates.Select(rate => rate.ToDto()).ToList();

        _logger.LogDebug(
            "Binance funding rates fetched. Symbol={Symbol}, StartTime={StartTime}, EndTime={EndTime}, Count={Count}",
            futuresSymbol,
            startTime,
            endTime,
            result.Count);

        return result;
    }

    private async Task<IReadOnlyList<CandleSnapshotDto>> GetCandlesAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance API error. StatusCode={StatusCode}, Endpoint={Endpoint}, Body={Body}",
                (int)response.StatusCode,
                url,
                body);

            MapErrorResponse(response.StatusCode, body, response.Headers.RetryAfter?.Delta);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Unexpected Binance kline response shape.");
        }

        var klines = new List<CandleSnapshotDto>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            klines.Add(BinanceKline.FromJsonArray(element).ToCandleSnapshotDto());
        }

        return klines;
    }

    private static string BuildKlinesUrl(string endpoint, string futuresSymbol, string interval, long startTime, long? endTime, int limit)
    {
        var url = $"{endpoint}?symbol={Uri.EscapeDataString(futuresSymbol)}&interval={Uri.EscapeDataString(interval)}&startTime={startTime}&limit={limit}";
        if (endTime.HasValue)
        {
            url += $"&endTime={endTime.Value}";
        }

        return url;
    }

    private static string BuildFundingRatesUrl(string futuresSymbol, long startTime, long? endTime, int limit)
    {
        var url = $"/fapi/v1/fundingRate?symbol={Uri.EscapeDataString(futuresSymbol)}&startTime={startTime}&limit={limit}";
        if (endTime.HasValue)
        {
            url += $"&endTime={endTime.Value}";
        }

        return url;
    }

    private static void MapErrorResponse(HttpStatusCode statusCode, string body, TimeSpan? retryAfter)
    {
        throw statusCode switch
        {
            HttpStatusCode.TooManyRequests => new RateLimitException(
                $"Binance rate limit exceeded: {body}",
                retryAfter is null ? null : (int)Math.Ceiling(retryAfter.Value.TotalSeconds)),
            (HttpStatusCode)451 => new DomainException($"Binance IP banned (451). Response: {body}"),
            _ when (int)statusCode >= 400 && (int)statusCode < 500 => new DomainException(
                $"Binance API error {(int)statusCode}: {body}"),
            _ => new DomainException($"Binance API server error {(int)statusCode}: {body}"),
        };
    }
}