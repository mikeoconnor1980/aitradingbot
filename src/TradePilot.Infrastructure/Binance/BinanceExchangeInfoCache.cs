using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceExchangeInfoCache : IBinanceExchangeInfoCache, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly IReadOnlyDictionary<string, int> MaxLeverageByAsset = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = 125,
        ["ETH"] = 125,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ConcurrentDictionary<string, BinanceExchangeSymbolMetadata>? _cache;
    private long _lastRefreshTimestamp;

    public BinanceExchangeInfoCache(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    public async Task<IReadOnlyDictionary<string, BinanceExchangeSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default)
        => await EnsureCacheAsync(cancellationToken);

    public async Task<BinanceExchangeSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default)
    {
        var cache = await EnsureCacheAsync(cancellationToken);
        cache.TryGetValue(BinanceAssetMapper.NormalizeSymbol(asset), out var metadata);
        return metadata;
    }

    private async Task<ConcurrentDictionary<string, BinanceExchangeSymbolMetadata>> EnsureCacheAsync(CancellationToken cancellationToken)
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

            var client = _httpClientFactory.CreateClient("binance-public");
            using var response = await client.GetAsync("/fapi/v1/exchangeInfo", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var symbols = document.RootElement.TryGetProperty("symbols", out var symbolsElement)
                ? JsonSerializer.Deserialize<List<BinanceExchangeInfoSymbol>>(symbolsElement.GetRawText()) ?? []
                : [];

            var refreshed = new ConcurrentDictionary<string, BinanceExchangeSymbolMetadata>(StringComparer.OrdinalIgnoreCase);

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
                var asset = BinanceAssetMapper.NormalizeSymbol(symbol.BaseAsset);

                refreshed[asset] = new BinanceExchangeSymbolMetadata(
                    asset,
                    symbol.Symbol,
                    GetDecimals(lotSizeFilter?.StepSize, 3),
                    GetDecimals(priceFilter?.TickSize, 2),
                    MaxLeverageByAsset.TryGetValue(asset, out var maxLeverage) ? maxLeverage : 25);
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
}