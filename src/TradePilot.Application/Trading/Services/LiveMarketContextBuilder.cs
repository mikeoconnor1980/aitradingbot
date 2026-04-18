using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.MacroCalendar.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Indicators;
using TradePilot.Indicators.Incremental;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Live <see cref="IMarketContextBuilder"/> that builds market context from real-time
/// candle data. Uses the same incremental indicator computation as the backtest
/// implementation but is fed candles from the CandleBuilder/CandleClock pipeline.
/// </summary>
public sealed class LiveMarketContextBuilder : IMarketContextBuilder
{
    private const int FastEmaPeriod = 20;
    private const int SlowEmaPeriod = 50;
    private const int TrendEmaPeriod = 200;

    private readonly List<Candle> _candles = [];
    private readonly List<(decimal High, decimal Low, decimal Close)> _bars = [];

    private readonly IncrementalEma _emaFast = new(FastEmaPeriod);
    private readonly IncrementalEma _emaSlow = new(SlowEmaPeriod);
    private readonly IncrementalEma _emaTrend = new(TrendEmaPeriod);
    private readonly IncrementalRsi _rsi14 = new(14);
    private readonly IncrementalAtr _atr14 = new(14);

    private readonly Dictionary<int, IncrementalEma> _dynamicEmas = new();
    private readonly Dictionary<int, IncrementalRsi> _dynamicRsis = new();
    private readonly Dictionary<int, IncrementalSma> _dynamicSmas = new();
    private readonly Dictionary<string, IncrementalMacd> _dynamicMacds = new(StringComparer.Ordinal);

    private readonly Dictionary<int, decimal?> _prevEma = new();
    private readonly Dictionary<int, decimal?> _prevRsi = new();
    private readonly Dictionary<int, decimal?> _prevSma = new();
    private readonly Dictionary<string, (decimal? Line, decimal? Signal, decimal? Histogram)> _prevMacd = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SupportResistanceResult?> _prevSr = new(StringComparer.Ordinal);

    private readonly SyntheticRegimeProvider _syntheticRegimeProvider = new();
    private readonly ILlmContextProvider? _llmContextProvider;
    private readonly IFearGreedSnapshotProvider? _fearGreedSnapshotProvider;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IHyperliquidRestClient? _restClient;
    private readonly IFearGreedReadingRepository? _fearGreedRepository;
    private readonly ILogger<LiveMarketContextBuilder>? _logger;
    private readonly ConcurrentDictionary<string, int> _maxLeverageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _metadataLock = new(1, 1);

    private bool _dynamicInitialized;

    public LiveMarketContextBuilder()
    {
    }

    public LiveMarketContextBuilder(
        ILlmContextProvider? llmContextProvider,
        IFearGreedSnapshotProvider? fearGreedSnapshotProvider,
        IServiceScopeFactory? serviceScopeFactory,
        IHyperliquidRestClient? restClient,
        ILogger<LiveMarketContextBuilder>? logger = null,
        IFearGreedReadingRepository? fearGreedRepository = null)
    {
        _llmContextProvider = llmContextProvider;
        _fearGreedSnapshotProvider = fearGreedSnapshotProvider;
        _serviceScopeFactory = serviceScopeFactory;
        _restClient = restClient;
        _logger = logger;
        _fearGreedRepository = fearGreedRepository;
    }

    public void UpdateIndicators(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        _candles.Add(candle);
        _bars.Add((candle.High, candle.Low, candle.Close));

        _emaFast.Add(candle.Close);
        _emaSlow.Add(candle.Close);
        _emaTrend.Add(candle.Close);
        _rsi14.Add(candle.Close);
        _atr14.Add(candle.High, candle.Low, candle.Close);
        _syntheticRegimeProvider.Update(_atr14.Current ?? 0m);

        if (_dynamicInitialized)
        {
            SnapshotDynamicPrevious();
            FeedDynamic(candle.Close);
        }
    }

    public MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle)
    {
        return Build(triggerCandle, latestOneHourCandle, latestFourHourCandle, null);
    }

    public MarketContext Build(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators)
    {
        ArgumentNullException.ThrowIfNull(triggerCandle);

        var indicatorContext = BuildIndicatorContext(requiredIndicators);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = _emaFast.Current ?? 0m,
            EmaSlow = _emaSlow.Current ?? 0m,
            EmaTrend = latestFourHourCandle?.Close ?? _emaTrend.Current ?? 0m,
            Rsi = _rsi14.Current ?? 50m,
            Atr = _atr14.Current ?? 0m
        };

        var llmContext = _syntheticRegimeProvider.Evaluate(indicators, triggerCandle.Timestamp);
        var maxLeverage = ResolveMaxLeverage(triggerCandle.Symbol);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            PreviousCandle = GetPreviousCandle(triggerCandle),
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = indicators,
            IndicatorContext = indicatorContext,
            LlmContext = llmContext,
            MaxLeverage = maxLeverage
        };
    }

    public async Task<MarketContext> BuildAsync(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggerCandle);

        var indicatorContext = BuildIndicatorContext(requiredIndicators);

        var indicators = new IndicatorSnapshot
        {
            EmaFast = _emaFast.Current ?? 0m,
            EmaSlow = _emaSlow.Current ?? 0m,
            EmaTrend = latestFourHourCandle?.Close ?? _emaTrend.Current ?? 0m,
            Rsi = _rsi14.Current ?? 50m,
            Atr = _atr14.Current ?? 0m
        };

        LlmContext? llmContext = null;
        var fearGreed = await ResolveFearGreedAsync(cancellationToken);

        if (_llmContextProvider is not null)
        {
            try
            {
                var upcomingEvents = await FetchUpcomingEventsAsync(cancellationToken);

                llmContext = await _llmContextProvider.GetContextAsync(
                    triggerCandle.Symbol,
                    indicators,
                    upcomingEvents,
                    fearGreed,
                    cancellationToken);

                if (llmContext is not null)
                {
                    await PersistSnapshotAsync(triggerCandle.Symbol, llmContext, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "LLM context provider failed; falling back to synthetic regime.");
            }
        }

        llmContext ??= _syntheticRegimeProvider.Evaluate(indicators, triggerCandle.Timestamp, fearGreed);
        var maxLeverage = await ResolveMaxLeverageAsync(triggerCandle.Symbol, cancellationToken);

        return new MarketContext
        {
            Symbol = triggerCandle.Symbol,
            TimestampUtc = triggerCandle.Timestamp,
            CurrentCandle = triggerCandle,
            PreviousCandle = GetPreviousCandle(triggerCandle),
            LatestOneHourCandle = latestOneHourCandle,
            LatestFourHourCandle = latestFourHourCandle,
            Indicators = indicators,
            IndicatorContext = indicatorContext,
            LlmContext = llmContext,
            FearGreed = fearGreed,
            MaxLeverage = maxLeverage
        };
    }

    private async Task<FearGreedSnapshot?> ResolveFearGreedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var providerSnapshot = await ResolveLatestFearGreedSnapshotAsync(cancellationToken);
            if (providerSnapshot is null)
            {
                return null;
            }

            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(providerSnapshot.TimestampUtc);
            if (age.TotalHours > 48)
            {
                _logger?.LogDebug("Fear & Greed reading is stale ({AgeHours:F1}h), skipping.", age.TotalHours);
                return null;
            }

            return providerSnapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to resolve Fear & Greed reading.");
            return null;
        }
    }

    private async Task<FearGreedSnapshot?> ResolveLatestFearGreedSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_fearGreedSnapshotProvider is not null)
        {
            var providedSnapshot = await _fearGreedSnapshotProvider.GetLatestAsync(cancellationToken);
            if (providedSnapshot is not null)
            {
                return providedSnapshot;
            }
        }

        var repo = _fearGreedRepository ?? ResolveFromScope<IFearGreedReadingRepository>();
        if (repo is null)
        {
            return null;
        }

        var latest = await repo.GetLatestAsync(cancellationToken);
        if (latest is null)
        {
            return null;
        }

        return new FearGreedSnapshot(latest.Value, FearGreedSnapshot.Classify(latest.Value), latest.Timestamp);
    }

    private T? ResolveFromScope<T>() where T : class
    {
        if (_serviceScopeFactory is null)
        {
            return null;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        return scope.ServiceProvider.GetService<T>();
    }

    private int? ResolveMaxLeverage(string symbol)
    {
        if (_restClient is null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var asset = NormalizeAsset(symbol);
        return _maxLeverageCache.TryGetValue(asset, out var cached) ? cached : null;
    }

    private async Task<int?> ResolveMaxLeverageAsync(string symbol, CancellationToken cancellationToken)
    {
        if (_restClient is null || string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var asset = NormalizeAsset(symbol);
        if (_maxLeverageCache.TryGetValue(asset, out var cachedMaxLeverage))
        {
            return cachedMaxLeverage;
        }

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_maxLeverageCache.TryGetValue(asset, out cachedMaxLeverage))
            {
                return cachedMaxLeverage;
            }

            var response = await _restClient.PostInfoAsync<JsonElement>(new { type = "meta" }, cancellationToken);
            if (response.TryGetProperty("universe", out var universe))
            {
                foreach (var item in universe.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var maxLeverage = item.TryGetProperty("maxLeverage", out var maxLeverageElement)
                        && maxLeverageElement.TryGetInt32(out var parsedMaxLeverage)
                        && parsedMaxLeverage > 0
                        ? parsedMaxLeverage
                        : LeverageCalculator.FallbackMaxLeverage;

                    _maxLeverageCache[name] = maxLeverage;
                }
            }

            return _maxLeverageCache.TryGetValue(asset, out cachedMaxLeverage)
                ? cachedMaxLeverage
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to resolve max leverage metadata for {Symbol}.", symbol);
            return null;
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private async Task<IReadOnlyCollection<MacroEventListItemDto>?> FetchUpcomingEventsAsync(
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return null;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetService<IMacroCalendarQueryService>();
            if (queryService is null)
            {
                return null;
            }

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var in24hMs = nowMs + (24L * 60 * 60 * 1000);

            return await queryService.GetUpcomingEventsAsync(
                nowMs,
                in24hMs,
                currency: null,
                minimumImportance: MacroEventImportance.Medium,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to fetch upcoming macro events for LLM context.");
            return null;
        }
    }

    private async Task PersistSnapshotAsync(
        string symbol,
        LlmContext llmContext,
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetService<ILlmContextSnapshotRepository>();
            if (repository is null)
            {
                return;
            }

            var snapshot = LlmContextSnapshot.Create(
                symbol,
                llmContext.MarketSentiment,
                llmContext.MacroRegime,
                llmContext.EventRisk,
                llmContext.Confidence,
                llmContext.DerivedRegime.ToString(),
                llmContext.Summary,
                llmContext.GeneratedAtUtc);

            await repository.SaveAsync(snapshot, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to persist LLM context snapshot.");
        }
    }

    private IndicatorContext? BuildIndicatorContext(IReadOnlyList<IndicatorRequirement>? requirements)
    {
        if (requirements is null || requirements.Count == 0)
        {
            return null;
        }

        EnsureDynamicInitialized(requirements);

        var context = new IndicatorContext();

        foreach (var requirement in requirements)
        {
            switch (requirement.Type.ToUpperInvariant())
            {
                case "RSI":
                    if (_dynamicRsis.TryGetValue(requirement.Period, out var rsi))
                    {
                        _prevRsi.TryGetValue(requirement.Period, out var prevRsi);
                        context.SetRsi(requirement.Period, rsi.Current ?? 50m, prevRsi);
                    }

                    break;

                case "EMA":
                    if (_dynamicEmas.TryGetValue(requirement.Period, out var ema))
                    {
                        _prevEma.TryGetValue(requirement.Period, out var prevEma);
                        context.SetEma(requirement.Period, ema.Current ?? 0m, prevEma);
                    }

                    break;

                case "SMA":
                    if (_dynamicSmas.TryGetValue(requirement.Period, out var sma))
                    {
                        _prevSma.TryGetValue(requirement.Period, out var prevSma);
                        context.SetSma(requirement.Period, sma.Current, prevSma);
                    }

                    break;

                case "MACD":
                {
                    var fast = requirement.FastPeriod ?? 12;
                    var slow = requirement.SlowPeriod ?? 26;
                    var signal = requirement.SignalPeriod ?? 9;
                    var key = MacdKey(fast, slow, signal);

                    if (_dynamicMacds.TryGetValue(key, out var macd) && macd.Line.HasValue)
                    {
                        _prevMacd.TryGetValue(key, out var prev);
                        context.SetMacd(
                            fast, slow, signal,
                            macd.Line.Value,
                            macd.Signal ?? 0m,
                            macd.Histogram ?? 0m,
                            prev.Line,
                            prev.Signal,
                            prev.Histogram);
                    }

                    break;
                }

                case "SUPPORT_RESISTANCE":
                {
                    var lookback = requirement.Lookback ?? 50;
                    var strength = requirement.Strength ?? 3;
                    var srKey = $"{lookback}_{strength}";
                    var srResult = SupportResistanceCalculator.Calculate(_bars, lookback, strength);
                    _prevSr.TryGetValue(srKey, out var previousSrResult);
                    _prevSr[srKey] = srResult;

                    if (srResult?.Support.HasValue == true)
                    {
                        context.SetSupport(lookback, srResult.Support.Value, previousSrResult?.Support);
                    }

                    if (srResult?.Resistance.HasValue == true)
                    {
                        context.SetResistance(lookback, srResult.Resistance.Value, previousSrResult?.Resistance);
                    }

                    break;
                }
            }
        }

        return context;
    }

    private void EnsureDynamicInitialized(IReadOnlyList<IndicatorRequirement> requirements)
    {
        if (_dynamicInitialized)
        {
            return;
        }

        foreach (var req in requirements)
        {
            switch (req.Type.ToUpperInvariant())
            {
                case "RSI":
                    _dynamicRsis.TryAdd(req.Period, new IncrementalRsi(req.Period));
                    break;
                case "EMA":
                    _dynamicEmas.TryAdd(req.Period, new IncrementalEma(req.Period));
                    break;
                case "SMA":
                    _dynamicSmas.TryAdd(req.Period, new IncrementalSma(req.Period));
                    break;
                case "MACD":
                {
                    var key = MacdKey(req.FastPeriod ?? 12, req.SlowPeriod ?? 26, req.SignalPeriod ?? 9);
                    _dynamicMacds.TryAdd(key, new IncrementalMacd(req.FastPeriod ?? 12, req.SlowPeriod ?? 26, req.SignalPeriod ?? 9));
                    break;
                }
            }
        }

        foreach (var candle in _candles)
        {
            SnapshotDynamicPrevious();
            FeedDynamic(candle.Close);
        }

        _dynamicInitialized = true;
    }

    private void SnapshotDynamicPrevious()
    {
        foreach (var (period, ema) in _dynamicEmas)
        {
            _prevEma[period] = ema.Current;
        }

        foreach (var (period, rsi) in _dynamicRsis)
        {
            _prevRsi[period] = rsi.Current;
        }

        foreach (var (period, sma) in _dynamicSmas)
        {
            _prevSma[period] = sma.Current;
        }

        foreach (var (key, macd) in _dynamicMacds)
        {
            _prevMacd[key] = (macd.Line, macd.Signal, macd.Histogram);
        }
    }

    private void FeedDynamic(decimal close)
    {
        foreach (var (_, ema) in _dynamicEmas)
        {
            ema.Add(close);
        }

        foreach (var (_, rsi) in _dynamicRsis)
        {
            rsi.Add(close);
        }

        foreach (var (_, sma) in _dynamicSmas)
        {
            sma.Add(close);
        }

        foreach (var (_, macd) in _dynamicMacds)
        {
            macd.Add(close);
        }
    }

    private Candle? GetPreviousCandle(Candle triggerCandle)
    {
        var triggerIndex = _candles.FindLastIndex(candle =>
            candle.Timestamp == triggerCandle.Timestamp
            && string.Equals(candle.Interval, triggerCandle.Interval, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candle.Symbol, triggerCandle.Symbol, StringComparison.OrdinalIgnoreCase));

        return triggerIndex > 0 ? _candles[triggerIndex - 1] : null;
    }

    private static string NormalizeAsset(string asset)
    {
        return asset.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase)
            ? asset[..^5]
            : asset;
    }

    private static string MacdKey(int fast, int slow, int signal) => $"{fast}_{slow}_{signal}";
}
