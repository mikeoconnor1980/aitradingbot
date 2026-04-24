using System.Diagnostics;
using System.Text.Json;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Hyperliquid;

public sealed class HyperliquidSymbolMetadataProvider : IExchangeSymbolMetadataProvider
{
    private const int DefaultSizeDecimals = 5;
    private const int DefaultMaxLeverage = 20;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHyperliquidRestClient _restClient;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<ExchangeSymbolMetadata>? _cachedSymbols;
    private long _lastRefreshTimestamp;

    public HyperliquidSymbolMetadataProvider(IHyperliquidRestClient restClient)
    {
        _restClient = restClient;
    }

    public async Task<IReadOnlyList<ExchangeSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSymbols is not null && Stopwatch.GetElapsedTime(_lastRefreshTimestamp) < CacheDuration)
        {
            return _cachedSymbols;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedSymbols is not null && Stopwatch.GetElapsedTime(_lastRefreshTimestamp) < CacheDuration)
            {
                return _cachedSymbols;
            }

            var response = await _restClient.PostInfoAsync<JsonElement>(new { type = "meta" }, cancellationToken);
            if (!response.TryGetProperty("universe", out var universe) || universe.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var symbols = new List<ExchangeSymbolMetadata>(universe.GetArrayLength());
            foreach (var asset in universe.EnumerateArray())
            {
                var metadata = Map(asset);
                if (metadata is not null)
                {
                    symbols.Add(metadata);
                }
            }

            _cachedSymbols = symbols;
            _lastRefreshTimestamp = Stopwatch.GetTimestamp();
            return symbols;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ExchangeSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);

        var symbols = await GetSupportedSymbolsAsync(cancellationToken);
        return symbols.FirstOrDefault(symbol =>
            string.Equals(symbol.Asset, asset, StringComparison.OrdinalIgnoreCase));
    }

    private static ExchangeSymbolMetadata? Map(JsonElement asset)
    {
        if (!asset.TryGetProperty("name", out var nameProperty))
        {
            return null;
        }

        var assetName = nameProperty.GetString();
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        var sizeDecimals = asset.TryGetProperty("szDecimals", out var sizeDecimalsProperty)
            && sizeDecimalsProperty.TryGetInt32(out var parsedSizeDecimals)
            && parsedSizeDecimals >= 0
            ? parsedSizeDecimals
            : DefaultSizeDecimals;

        var maxLeverage = asset.TryGetProperty("maxLeverage", out var maxLeverageProperty)
            && maxLeverageProperty.TryGetInt32(out var parsedMaxLeverage)
            && parsedMaxLeverage > 0
            ? parsedMaxLeverage
            : DefaultMaxLeverage;

        return new ExchangeSymbolMetadata(
            assetName,
            assetName,
            sizeDecimals,
            GetPerpPriceDecimals(sizeDecimals),
            maxLeverage);
    }

    private static int GetPerpPriceDecimals(int sizeDecimals)
        => Math.Max(0, 6 - sizeDecimals);
}