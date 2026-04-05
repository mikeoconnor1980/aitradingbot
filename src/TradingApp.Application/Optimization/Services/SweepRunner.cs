using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Optimization.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Optimization.Services;

public sealed record SweepResult(
    IReadOnlyList<RankedResult> TopResults,
    int TotalRun,
    int TotalQualified,
    int TotalFailed,
    long ElapsedMs);

public sealed record RankedResult(
    int Rank,
    decimal FitnessScore,
    GeneratedStrategy Strategy,
    BacktestResult BacktestResult,
    FitnessMetrics? Metrics = null,
    OutOfSampleMetrics? OutOfSample = null);

public sealed record SweepProgress(int Completed, int Total, string Phase, long? EstimatedRemainingMs);

public interface ISweepRunner
{
    Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<SweepProgress>? onProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs parameter sweep optimizations by creating an isolated DI scope per
/// backtest so that stateful services (indicators, positions, execution engine)
/// are not shared across parallel runs.
/// </summary>
public sealed class SweepRunner : ISweepRunner
{
    private static readonly string[] RequiredIntervals = ["15m", "1h", "4h"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICandleRepository _candleRepository;
    private readonly IStrategyConfigGenerator _configGenerator;
    private readonly IFitnessScorer _fitnessScorer;
    private readonly ILogger<SweepRunner> _logger;

    public SweepRunner(
        IServiceScopeFactory scopeFactory,
        ICandleRepository candleRepository,
        IStrategyConfigGenerator configGenerator,
        IFitnessScorer fitnessScorer,
        ILogger<SweepRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _candleRepository = candleRepository;
        _configGenerator = configGenerator;
        _fitnessScorer = fitnessScorer;
        _logger = logger;
    }

    public async Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<SweepProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // --- Phase 1: Initial random generation ---------------------------
        var generatedStrategies = _configGenerator.Generate(config.Symbol, config.Bounds, config.SampleSize);

        // Calculate walk-forward date boundary
        var walkForwardEnabled = config.WalkForward.Enabled
            && config.WalkForward.ValidationSplitPercent is > 0m and < 100m;
        var (inSampleStart, inSampleEnd, oosStart, oosEnd) = walkForwardEnabled
            ? SplitDateRange(config.StartDateUtc, config.EndDateUtc, config.WalkForward.ValidationSplitPercent)
            : (config.StartDateUtc, config.EndDateUtc, 0L, 0L);

        var qualifiedResults = new ConcurrentBag<(GeneratedStrategy Strategy, BacktestResult Result, decimal Score)>();
        var counters = new SweepCounters();

        var totalWork = CalculateTotalWork(config, generatedStrategies.Count);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism > 0
                ? config.MaxDegreeOfParallelism
                : Environment.ProcessorCount,
        };

        // --- Phase 1 sweep (in-sample period) -----------------------------
        await RunSweepAsync(generatedStrategies, config, inSampleStart, inSampleEnd, qualifiedResults, parallelOptions,
            counters, totalWork, "Sweep", stopwatch, onProgress, cancellationToken);

        // --- Phase 2: Evolutionary generations ----------------------------
        if (config.Evolutionary.Enabled && config.Evolutionary.Generations > 0)
        {
            var evolutionaryRunner = new EvolutionaryRunner(_configGenerator, _logger);

            for (var gen = 0; gen < config.Evolutionary.Generations; gen++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var eliteStrategies = qualifiedResults
                    .OrderByDescending(item => item.Score)
                    .Take(config.Evolutionary.EliteCount)
                    .Select(item => item.Strategy)
                    .ToList();

                if (eliteStrategies.Count < 2)
                {
                    _logger.LogInformation("Evolutionary generation {Generation} skipped: fewer than 2 elite strategies", gen + 1);
                    break;
                }

                var offspring = evolutionaryRunner.Breed(
                    eliteStrategies,
                    config.Symbol,
                    config.Bounds,
                    config.SampleSize,
                    config.Evolutionary.CrossoverRate,
                    config.Evolutionary.MutationRate,
                    gen + 1);

                _logger.LogInformation(
                    "Evolutionary generation {Generation}: breeding {EliteCount} elites → {OffspringCount} offspring",
                    gen + 1, eliteStrategies.Count, offspring.Count);

                await RunSweepAsync(offspring, config, inSampleStart, inSampleEnd, qualifiedResults, parallelOptions,
                    counters, totalWork, $"Evolution Gen {gen + 1}", stopwatch, onProgress, cancellationToken);
            }
        }

        // --- Phase 3: Walk-forward out-of-sample validation ---------------
        var rankedInSample = qualifiedResults
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Result.TotalPnL)
            .Take(25)
            .ToList();

        List<RankedResult> ranked;

        if (walkForwardEnabled && rankedInSample.Count > 0)
        {
            _logger.LogInformation(
                "Walk-forward validation: running {Count} top strategies on OOS period ({OosStart}–{OosEnd})",
                rankedInSample.Count, oosStart, oosEnd);

            ranked = await RunWalkForwardValidationAsync(
                rankedInSample, config, oosStart, oosEnd, parallelOptions,
                counters, totalWork, stopwatch, onProgress, cancellationToken);
        }
        else
        {
            ranked = rankedInSample
                .Select((item, index) => new RankedResult(index + 1, item.Score, item.Strategy, item.Result, _fitnessScorer.ComputeMetrics(item.Result)))
                .ToList();
        }

        stopwatch.Stop();

        if (counters.Failed > 0)
        {
            _logger.LogWarning("Optimization sweep completed with {FailedCount}/{TotalCount} failed backtests", counters.Failed, counters.Completed);
        }

        return new SweepResult(ranked, counters.Completed, qualifiedResults.Count, counters.Failed, stopwatch.ElapsedMilliseconds);
    }

    private async Task RunSweepAsync(
        IReadOnlyList<GeneratedStrategy> strategies,
        SweepConfig config,
        long startDateUtc,
        long endDateUtc,
        ConcurrentBag<(GeneratedStrategy Strategy, BacktestResult Result, decimal Score)> qualifiedResults,
        ParallelOptions parallelOptions,
        SweepCounters counters,
        int totalWork,
        string phase,
        System.Diagnostics.Stopwatch stopwatch,
        Action<SweepProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        var backtestSymbol = string.IsNullOrWhiteSpace(config.BacktestSymbol)
            ? config.Symbol
            : config.BacktestSymbol;

        // Pre-load candle data per unique timeframe to avoid redundant DB queries
        var replayCache = await PreloadReplayDataAsync(
            strategies, backtestSymbol, startDateUtc, endDateUtc, config.InitialCapital, cancellationToken);

        await Parallel.ForEachAsync(strategies, parallelOptions, async (generatedStrategy, token) =>
        {
            try
            {
                var backtestConfig = new BacktestConfig
                {
                    Symbol = backtestSymbol,
                    Intervals = RequiredIntervals,
                    StartDateUtc = startDateUtc,
                    EndDateUtc = endDateUtc,
                    InitialCapital = config.InitialCapital,
                    Strategy = generatedStrategy.Config,
                    Execution = new ExecutionConfig(),
                    EnableAuditLog = false,
                    TriggerTimeframe = generatedStrategy.Config.Timeframe,
                };

                await using var scope = _scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<IBacktestRunner>();

                var result = replayCache.TryGetValue(generatedStrategy.Config.Timeframe, out var preloaded)
                    ? await runner.RunAsync(backtestConfig, preloaded, token)
                    : await runner.RunAsync(backtestConfig, token);

                if (_fitnessScorer.IsQualified(result, config.Thresholds, config.InitialCapital))
                {
                    qualifiedResults.Add((generatedStrategy, result, _fitnessScorer.Score(result)));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failCount = counters.IncrementFailed();
                if (failCount <= 5)
                {
                    _logger.LogWarning(ex, "Backtest failed for strategy '{Description}'", generatedStrategy.Description);
                }
                else if (failCount == 6)
                {
                    _logger.LogWarning("Suppressing further backtest failure logs ({FailedCount} failures so far)", failCount);
                }
            }
            finally
            {
                var current = counters.IncrementCompleted();
                onProgress?.Invoke(new SweepProgress(current, totalWork, phase, EstimateRemainingMs(stopwatch, current, totalWork)));
            }
        });
    }

    private async Task<List<RankedResult>> RunWalkForwardValidationAsync(
        List<(GeneratedStrategy Strategy, BacktestResult Result, decimal Score)> inSampleRanked,
        SweepConfig config,
        long oosStart,
        long oosEnd,
        ParallelOptions parallelOptions,
        SweepCounters counters,
        int totalWork,
        System.Diagnostics.Stopwatch stopwatch,
        Action<SweepProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        var oosResults = new ConcurrentDictionary<string, (BacktestResult Result, decimal Score)>();

        var backtestSymbol = string.IsNullOrWhiteSpace(config.BacktestSymbol)
            ? config.Symbol
            : config.BacktestSymbol;

        // Pre-load candle data for OOS period
        var oosStrategies = inSampleRanked.Select(item => item.Strategy).ToList();
        var replayCache = await PreloadReplayDataAsync(
            oosStrategies, backtestSymbol, oosStart, oosEnd, config.InitialCapital, cancellationToken);

        await Parallel.ForEachAsync(inSampleRanked, parallelOptions, async (item, token) =>
        {
            try
            {
                var backtestConfig = new BacktestConfig
                {
                    Symbol = backtestSymbol,
                    Intervals = RequiredIntervals,
                    StartDateUtc = oosStart,
                    EndDateUtc = oosEnd,
                    InitialCapital = config.InitialCapital,
                    Strategy = item.Strategy.Config,
                    Execution = new ExecutionConfig(),
                    EnableAuditLog = false,
                    TriggerTimeframe = item.Strategy.Config.Timeframe,
                };

                await using var scope = _scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<IBacktestRunner>();

                var result = replayCache.TryGetValue(item.Strategy.Config.Timeframe, out var preloaded)
                    ? await runner.RunAsync(backtestConfig, preloaded, token)
                    : await runner.RunAsync(backtestConfig, token);

                oosResults[item.Strategy.Config.StrategyName] = (result, _fitnessScorer.Score(result));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failCount = counters.IncrementFailed();
                if (failCount <= 5)
                {
                    _logger.LogWarning(ex, "OOS backtest failed for strategy '{Description}'", item.Strategy.Description);
                }
            }
            finally
            {
                var current = counters.IncrementCompleted();
                onProgress?.Invoke(new SweepProgress(current, totalWork, "OOS Validation", EstimateRemainingMs(stopwatch, current, totalWork)));
            }
        });

        // Rank by combined in-sample + OOS fitness (average), with OOS fitness > 0 required
        var ranked = inSampleRanked
            .Select(item =>
            {
                OutOfSampleMetrics? oos = null;
                if (oosResults.TryGetValue(item.Strategy.Config.StrategyName, out var oosResult))
                {
                    oos = new OutOfSampleMetrics
                    {
                        TotalPnl = oosResult.Result.TotalPnL,
                        WinRate = oosResult.Result.WinRate,
                        MaxDrawdown = oosResult.Result.MaxDrawdownAbsolute,
                        TotalTrades = oosResult.Result.TotalTrades,
                        FitnessScore = oosResult.Score,
                    };
                }

                // Combined score: 60% in-sample + 40% OOS (penalize strategies with no OOS data)
                var combinedScore = oos is not null
                    ? (item.Score * 0.6m) + (oos.FitnessScore * 0.4m)
                    : item.Score * 0.3m;

                return (item.Strategy, item.Result, CombinedScore: combinedScore, OutOfSample: oos);
            })
            .OrderByDescending(item => item.CombinedScore)
            .Select((item, index) => new RankedResult(
                index + 1,
                item.CombinedScore,
                item.Strategy,
                item.Result,
                _fitnessScorer.ComputeMetrics(item.Result),
                item.OutOfSample))
            .ToList();

        return ranked;
    }

    private async Task<Dictionary<string, ReplayData>> PreloadReplayDataAsync(
        IReadOnlyList<GeneratedStrategy> strategies,
        string symbol,
        long startDateUtc,
        long endDateUtc,
        decimal initialCapital,
        CancellationToken cancellationToken)
    {
        var uniqueTimeframes = strategies
            .Select(s => s.Config.Timeframe)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cache = new Dictionary<string, ReplayData>(StringComparer.OrdinalIgnoreCase);
        var replayEngine = new CandleReplayEngine(_candleRepository);

        foreach (var timeframe in uniqueTimeframes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var probeConfig = new BacktestConfig
            {
                Symbol = symbol,
                Intervals = RequiredIntervals,
                StartDateUtc = startDateUtc,
                EndDateUtc = endDateUtc,
                InitialCapital = initialCapital,
                Strategy = strategies.First(s => s.Config.Timeframe.Equals(timeframe, StringComparison.OrdinalIgnoreCase)).Config,
                Execution = new ExecutionConfig(),
                EnableAuditLog = false,
                TriggerTimeframe = timeframe,
            };

            try
            {
                var replayData = await replayEngine.LoadAsync(probeConfig, cancellationToken);
                cache[timeframe] = replayData;
                _logger.LogInformation(
                    "Pre-loaded {Timeframe} candle data: {TriggerCount} trigger candles, {Candles1h} 1h, {Candles4h} 4h",
                    timeframe, replayData.TriggerCandles.Count, replayData.Candles1h.Count, replayData.Candles4h.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-load candle data for timeframe {Timeframe}, iterations will load individually", timeframe);
            }
        }

        return cache;
    }

    private static (long InSampleStart, long InSampleEnd, long OosStart, long OosEnd) SplitDateRange(
        long startUtc, long endUtc, decimal validationSplitPercent)
    {
        var totalMs = endUtc - startUtc;
        var inSampleMs = (long)(totalMs * (1m - validationSplitPercent / 100m));
        var splitPoint = startUtc + inSampleMs;

        return (startUtc, splitPoint, splitPoint, endUtc);
    }

    private static int CalculateTotalWork(SweepConfig config, int initialStrategies)
    {
        var total = initialStrategies;

        if (config.Evolutionary.Enabled && config.Evolutionary.Generations > 0)
        {
            total += config.Evolutionary.Generations * config.SampleSize;
        }

        if (config.WalkForward.Enabled && config.WalkForward.ValidationSplitPercent is > 0m and < 100m)
        {
            total += 10; // Top 10 OOS runs
        }

        return total;
    }

    private static long? EstimateRemainingMs(System.Diagnostics.Stopwatch stopwatch, int completed, int total)
    {
        if (completed <= 0 || total <= 0)
        {
            return null;
        }

        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var msPerItem = (double)elapsedMs / completed;
        var remaining = (long)(msPerItem * (total - completed));

        return remaining;
    }

    private sealed class SweepCounters
    {
        private int _completed;
        private int _failed;

        public int Completed => _completed;
        public int Failed => _failed;

        public int IncrementCompleted() => Interlocked.Increment(ref _completed);
        public int IncrementFailed() => Interlocked.Increment(ref _failed);
    }
}