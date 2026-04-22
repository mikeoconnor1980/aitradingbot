using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Hyperliquid.Models;

namespace TradePilot.Infrastructure.Services;

public sealed class HyperliquidRestClient : IHyperliquidRestClient
{
    // Hyperliquid uses case-sensitive JSON keys (e.g. "t" vs "T" in candle responses).
    // System.Text.Json defaults to case-insensitive matching which causes a property
    // name collision at type-info build time. Case-sensitive options fix this.
    private static readonly JsonSerializerOptions CaseSensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HyperliquidRestClient> _logger;

    public HyperliquidRestClient(HttpClient httpClient, ILogger<HyperliquidRestClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/info",
                new { type = "meta" },
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<TResponse> PostInfoAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/info",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            _logger.LogWarning(
                "Hyperliquid API error. StatusCode={StatusCode}, Endpoint=/info, Body={ErrorBody}",
                statusCode,
                errorBody);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                int? retryAfter = response.Headers.RetryAfter?.Delta is { } delta
                    ? (int)delta.TotalSeconds
                    : null;

                throw new RateLimitException(
                    $"Hyperliquid rate limit exceeded: {errorBody}",
                    retryAfter);
            }

            var clientMessage = statusCode >= 500
                ? "Hyperliquid exchange returned a server error"
                : $"Hyperliquid API error: {errorBody}";

            throw new HyperliquidApiException(
                clientMessage,
                statusCode,
                statusCode >= 500 ? "exchange_error" : "validation_error");
        }

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(CaseSensitiveOptions, cancellationToken);
        if (payload is null)
        {
            throw new HttpRequestException("Hyperliquid API returned an empty response body.");
        }

        return payload;
    }

    public async Task<TResponse> PostExchangeAsync<TResponse>(
        object signedPayload,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/exchange",
            signedPayload,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;

            _logger.LogWarning(
                "Hyperliquid exchange error. StatusCode={StatusCode}, Endpoint=/exchange, Body={ResponseBody}",
                statusCode,
                responseBody);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                int? retryAfter = response.Headers.RetryAfter?.Delta is { } delta
                    ? (int)delta.TotalSeconds
                    : null;

                throw new RateLimitException(
                    $"Hyperliquid rate limit exceeded: {responseBody}",
                    retryAfter);
            }

            var clientMessage = statusCode >= 500
                ? "Hyperliquid exchange returned a server error"
                : $"Hyperliquid exchange error: {responseBody}";

            throw new HyperliquidApiException(
                clientMessage,
                statusCode,
                statusCode >= 500 ? "exchange_error" : "validation_error");
        }

        _logger.LogDebug("Hyperliquid exchange raw response: {ResponseBody}", responseBody);

        TResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<TResponse>(responseBody, CaseSensitiveOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Hyperliquid exchange response. Body: {ResponseBody}", responseBody);
            throw;
        }

        if (result is null)
        {
            throw new InvalidOperationException($"Failed to deserialize exchange response: {responseBody}");
        }

        return result;
    }

    public async Task<MarketInfoDto?> GetMarketInfoAsync(
        string asset,
        CancellationToken cancellationToken = default)
    {
        var coin = HyperliquidAssetMapper.ToCoin(asset);
        var request = new HyperliquidInfoRequest { Type = "metaAndAssetCtxs" };

        var response = await PostInfoAsync<JsonElement>(request, cancellationToken);
        if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() < 2)
        {
            throw new HttpRequestException("Unexpected response shape from Hyperliquid metaAndAssetCtxs endpoint.");
        }

        var meta = response[0].Deserialize<HyperliquidMeta>();
        if (meta is null)
        {
            throw new HttpRequestException("Unable to deserialize Hyperliquid universe metadata.");
        }

        var assetCtxs = response[1];
        if (assetCtxs.ValueKind != JsonValueKind.Array)
        {
            throw new HttpRequestException("Unexpected asset contexts shape from Hyperliquid metaAndAssetCtxs endpoint.");
        }

        var assetIndex = -1;
        for (var i = 0; i < meta.Universe.Count; i++)
        {
            if (string.Equals(meta.Universe[i].Name, coin, StringComparison.OrdinalIgnoreCase))
            {
                assetIndex = i;
                break;
            }
        }

        if (assetIndex < 0 || assetIndex >= assetCtxs.GetArrayLength())
        {
            _logger.LogWarning("Asset {Asset} mapped to {Coin} was not returned by Hyperliquid universe.", asset, coin);
            return null;
        }

        var ctx = assetCtxs[assetIndex].Deserialize<HyperliquidAssetCtx>();
        if (ctx is null)
        {
            throw new HttpRequestException($"Unable to deserialize asset context for '{asset}'.");
        }

        var midPrice = HyperliquidFormatting.ParseDecimal(ctx.MidPx);
        var prevDayPrice = HyperliquidFormatting.ParseDecimal(ctx.PrevDayPx);
        var priceChangePercent = prevDayPrice == 0m
            ? 0m
            : ((midPrice - prevDayPrice) / prevDayPrice) * 100m;

        return new MarketInfoDto
        {
            Asset = asset,
            MidPrice = midPrice,
            MarkPrice = HyperliquidFormatting.ParseDecimal(ctx.MarkPx),
            IndexPrice = HyperliquidFormatting.ParseDecimal(ctx.OraclePx),
            FundingRate = HyperliquidFormatting.ParseDecimal(ctx.Funding),
            Volume24h = HyperliquidFormatting.ParseDecimal(ctx.DayNtlVlm),
            OpenInterest = HyperliquidFormatting.ParseDecimal(ctx.OpenInterest),
            PriceChange24hPercent = Math.Round(priceChangePercent, 2),
        };
    }

    public async Task<List<CandleDto>> GetCandlesAsync(
        string asset,
        string timeframe,
        long? endTime = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTimeframe = timeframe.ToLowerInvariant();
        var intervalMs = HyperliquidAssetMapper.GetIntervalMs(normalizedTimeframe);

        var end = endTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = end - (500L * intervalMs);
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        var request = new HyperliquidCandleSnapshotRequest
        {
            Req = new CandleSnapshotPayload
            {
                Coin = coin,
                Interval = normalizedTimeframe,
                StartTime = startTime,
                EndTime = end,
            },
        };

        var candles = await PostInfoAsync<List<HyperliquidCandle>>(request, cancellationToken);

        return candles
            .Select(c => new CandleDto
            {
                Timestamp = c.OpenTime,
                Open = HyperliquidFormatting.ParseDecimal(c.Open),
                High = HyperliquidFormatting.ParseDecimal(c.High),
                Low = HyperliquidFormatting.ParseDecimal(c.Low),
                Close = HyperliquidFormatting.ParseDecimal(c.Close),
                Volume = HyperliquidFormatting.ParseDecimal(c.Volume),
            })
            .OrderByDescending(c => c.Timestamp)
            .Take(500)
            .ToList();
    }

    public async Task<List<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        string asset,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        var normalizedTimeframe = timeframe.ToLowerInvariant();
        _ = HyperliquidAssetMapper.GetIntervalMs(normalizedTimeframe);
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        var request = new HyperliquidCandleSnapshotRequest
        {
            Req = new CandleSnapshotPayload
            {
                Coin = coin,
                Interval = normalizedTimeframe,
                StartTime = startTime,
                EndTime = endTime,
            },
        };

        var candles = await PostInfoAsync<List<HyperliquidCandle>>(request, cancellationToken);

        return candles
            .Select(c => new CandleSnapshotDto
            {
                Timestamp = c.OpenTime,
                Open = HyperliquidFormatting.ParseDecimal(c.Open),
                High = HyperliquidFormatting.ParseDecimal(c.High),
                Low = HyperliquidFormatting.ParseDecimal(c.Low),
                Close = HyperliquidFormatting.ParseDecimal(c.Close),
                Volume = HyperliquidFormatting.ParseDecimal(c.Volume),
                NumTrades = c.NumTrades,
            })
            .ToList();
    }

    public async Task<List<FillEventDto>> GetUserFillsAsync(
        string walletAddress,
        long? startTimeMs = null,
        CancellationToken cancellationToken = default)
    {
        object request = startTimeMs.HasValue
            ? new { type = "userFillsByTime", user = walletAddress, startTime = startTimeMs.Value }
            : new { type = "userFills", user = walletAddress };

        var fills = await PostInfoAsync<List<HyperliquidUserFill>>(request, cancellationToken);

        return fills
            .Select(f => new FillEventDto
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(f.TimestampMs).UtcDateTime,
                Asset = f.Coin,
                Side = HyperliquidFormatting.MapOrderSide(f.Side),
                Direction = f.Direction,
                Size = HyperliquidFormatting.ParseDecimal(f.Size),
                Price = HyperliquidFormatting.ParseDecimal(f.Price),
                Fee = HyperliquidFormatting.ParseDecimal(f.Fee),
                ClosedPnl = HyperliquidFormatting.ParseDecimal(f.ClosedPnl),
                OrderId = f.OrderId.ToString()
            })
            .OrderByDescending(f => f.Timestamp)
            .ToList();
    }
}
