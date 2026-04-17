using System.Collections.Concurrent;
using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Services;

public sealed record AssetMetadata(int Index, int SzDecimals, int MaxLeverage);

public interface IHyperliquidAssetMetadataCache
{
    Task<AssetMetadata> GetAsync(string coin, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, AssetMetadata>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class HyperliquidAssetMetadataCache : IHyperliquidAssetMetadataCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHyperliquidRestClient _restClient;
    private readonly ILogger<HyperliquidAssetMetadataCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ConcurrentDictionary<string, AssetMetadata>? _cache;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public HyperliquidAssetMetadataCache(
        IHyperliquidRestClient restClient,
        ILogger<HyperliquidAssetMetadataCache> logger)
    {
        _restClient = restClient;
        _logger = logger;
    }

    public async Task<AssetMetadata> GetAsync(string coin, CancellationToken cancellationToken = default)
    {
        var cache = await EnsureCacheAsync(cancellationToken);

        if (cache.TryGetValue(coin, out var metadata))
        {
            return metadata;
        }

        throw new NotFoundException("Asset", coin);
    }

    public async Task<IReadOnlyDictionary<string, AssetMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cache = await EnsureCacheAsync(cancellationToken);
        return cache;
    }

    private async Task<ConcurrentDictionary<string, AssetMetadata>> EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null && DateTimeOffset.UtcNow < _cacheExpiry)
        {
            return _cache;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null && DateTimeOffset.UtcNow < _cacheExpiry)
            {
                return _cache;
            }

            var response = await _restClient.PostInfoAsync<JsonElement>(new { type = "meta" }, cancellationToken);
            var newCache = new ConcurrentDictionary<string, AssetMetadata>(StringComparer.OrdinalIgnoreCase);

            if (response.TryGetProperty("universe", out var universe))
            {
                var index = 0;
                foreach (var item in universe.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString()!;
                    var szDecimals = item.TryGetProperty("szDecimals", out var szd) ? szd.GetInt32() : 5;
                    var maxLeverage = item.TryGetProperty("maxLeverage", out var ml) ? ml.GetInt32() : 20;

                    newCache[name] = new AssetMetadata(index, szDecimals, maxLeverage);
                    index++;
                }
            }

            _logger.LogInformation("Cached {Count} asset metadata entries from Hyperliquid meta API", newCache.Count);
            _cache = newCache;
            _cacheExpiry = DateTimeOffset.UtcNow.Add(CacheDuration);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }
}
