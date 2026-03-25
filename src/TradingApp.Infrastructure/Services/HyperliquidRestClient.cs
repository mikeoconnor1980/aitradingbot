using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Infrastructure.Services;

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
            throw new HttpRequestException(
                $"Hyperliquid API error {(int)response.StatusCode}: {errorBody}",
                inner: null,
                response.StatusCode);
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
            throw new HttpRequestException(
                $"Hyperliquid exchange returned {response.StatusCode}: {responseBody}",
                null,
                response.StatusCode);
        }

        _logger.LogInformation("Hyperliquid exchange raw response: {ResponseBody}", responseBody);

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

        var midPrice = ParseDecimal(ctx.MidPx);
        var prevDayPrice = ParseDecimal(ctx.PrevDayPx);
        var priceChangePercent = prevDayPrice == 0m
            ? 0m
            : ((midPrice - prevDayPrice) / prevDayPrice) * 100m;

        return new MarketInfoDto
        {
            Asset = asset,
            MidPrice = midPrice,
            MarkPrice = ParseDecimal(ctx.MarkPx),
            IndexPrice = ParseDecimal(ctx.OraclePx),
            FundingRate = ParseDecimal(ctx.Funding),
            Volume24h = ParseDecimal(ctx.DayNtlVlm),
            OpenInterest = ParseDecimal(ctx.OpenInterest),
            PriceChange24hPercent = Math.Round(priceChangePercent, 2),
        };
    }

    public async Task<List<CandleDto>> GetCandlesAsync(
        string asset,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var normalizedTimeframe = timeframe.ToLowerInvariant();
        var intervalMs = HyperliquidAssetMapper.GetIntervalMs(normalizedTimeframe);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = now - (96L * intervalMs);
        var coin = HyperliquidAssetMapper.ToCoin(asset);

        var request = new HyperliquidCandleSnapshotRequest
        {
            Req = new CandleSnapshotPayload
            {
                Coin = coin,
                Interval = normalizedTimeframe,
                StartTime = startTime,
                EndTime = now,
            },
        };

        var candles = await PostInfoAsync<List<HyperliquidCandle>>(request, cancellationToken);

        return candles
            .Select(c => new CandleDto
            {
                Timestamp = c.OpenTime,
                Open = ParseDecimal(c.Open),
                High = ParseDecimal(c.High),
                Low = ParseDecimal(c.Low),
                Close = ParseDecimal(c.Close),
                Volume = ParseDecimal(c.Volume),
            })
            .OrderByDescending(c => c.Timestamp)
            .Take(96)
            .ToList();
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"Unable to parse Hyperliquid decimal value: '{value}'");
    }
}
