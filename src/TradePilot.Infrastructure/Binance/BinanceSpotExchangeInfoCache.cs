using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSpotExchangeInfoCache : IBinanceSpotExchangeInfoCache, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ConcurrentDictionary<string, BinanceSpotSymbolMetadata>? _cache;
    private long _lastRefreshTimestamp;

    public BinanceSpotExchangeInfoCache(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    public async Task<IReadOnlyDictionary<string, BinanceSpotSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default)
        => await EnsureCacheAsync(cancellationToken);

    public async Task<BinanceSpotSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default)
    {
        var cache = await EnsureCacheAsync(cancellationToken);
        cache.TryGetValue(BinanceAssetMapper.NormalizeSymbol(asset), out var metadata);
        return metadata;
    }

    private async Task<ConcurrentDictionary<string, BinanceSpotSymbolMetadata>> EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (IsCacheFresh())
        {
            return _cache!;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsCacheFresh())
            {
                return _cache!;
            }

            var client = _httpClientFactory.CreateClient("binance-spot-public");
            using var response = await client.GetAsync("/api/v3/exchangeInfo", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var symbols = document.RootElement.TryGetProperty("symbols", out var symbolsElement)
                ? JsonSerializer.Deserialize<List<SpotExchangeInfoSymbol>>(symbolsElement.GetRawText()) ?? []
                : [];

            var refreshed = new ConcurrentDictionary<string, BinanceSpotSymbolMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols)
            {
                if (!string.Equals(symbol.Status, "TRADING", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(symbol.QuoteAsset, "USDT", StringComparison.OrdinalIgnoreCase) ||
                    !BinanceAssetMapper.IsValidSymbol(symbol.BaseAsset))
                {
                    continue;
                }

                var lotSizeFilter = symbol.Filters.FirstOrDefault(filter => string.Equals(filter.FilterType, "LOT_SIZE", StringComparison.OrdinalIgnoreCase));
                var priceFilter = symbol.Filters.FirstOrDefault(filter => string.Equals(filter.FilterType, "PRICE_FILTER", StringComparison.OrdinalIgnoreCase));
                var notionalFilter = symbol.Filters.FirstOrDefault(filter => string.Equals(filter.FilterType, "NOTIONAL", StringComparison.OrdinalIgnoreCase));
                var asset = BinanceAssetMapper.NormalizeSymbol(symbol.BaseAsset);

                refreshed[asset] = new BinanceSpotSymbolMetadata(
                    asset,
                    symbol.Symbol,
                    GetDecimals(lotSizeFilter?.StepSize, 3),
                    GetDecimals(priceFilter?.TickSize, 2),
                    ParseMinNotional(notionalFilter?.MinNotional, 10m));
            }

            _cache = refreshed;
            _lastRefreshTimestamp = Stopwatch.GetTimestamp();
            return refreshed;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsCacheFresh()
        => _cache is not null && Stopwatch.GetElapsedTime(_lastRefreshTimestamp) < CacheDuration;

    private static int GetDecimals(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            return fallback;
        }

        var text = parsed.ToString("G29", CultureInfo.InvariantCulture);
        var separatorIndex = text.IndexOf('.');
        return separatorIndex >= 0 ? text.Length - separatorIndex - 1 : 0;
    }

    private static decimal ParseMinNotional(string? value, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            return fallback;
        }

        return parsed;
    }

    private sealed class SpotExchangeInfoSymbol
    {
        public string Symbol { get; init; } = string.Empty;
        public string BaseAsset { get; init; } = string.Empty;
        public string QuoteAsset { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public List<SpotExchangeInfoFilter> Filters { get; init; } = [];
    }

    private sealed class SpotExchangeInfoFilter
    {
        public string FilterType { get; init; } = string.Empty;
        public string? StepSize { get; init; }
        public string? TickSize { get; init; }
        public string? MinNotional { get; init; }
    }
}
