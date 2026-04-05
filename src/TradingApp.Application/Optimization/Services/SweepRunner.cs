using System.Collections.Concurrent;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Optimization.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Optimization.Services;

public sealed record SweepResult(
    IReadOnlyList<RankedResult> TopResults,
    int TotalRun,
    int TotalQualified,
    long ElapsedMs);

public sealed record RankedResult(
    int Rank,
    decimal FitnessScore,
    GeneratedStrategy Strategy,
    BacktestResult BacktestResult);

public interface ISweepRunner
{
    Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default);
}

public sealed class SweepRunner : ISweepRunner
{
    private static readonly string[] RequiredIntervals = ["15m", "1h", "4h"];

    private readonly IBacktestRunner _backtestRunner;
    private readonly IStrategyConfigGenerator _configGenerator;
    private readonly IFitnessScorer _fitnessScorer;

    public SweepRunner(
        IBacktestRunner backtestRunner,
        IStrategyConfigGenerator configGenerator,
        IFitnessScorer fitnessScorer)
    {
        _backtestRunner = backtestRunner;
        _configGenerator = configGenerator;
        _fitnessScorer = fitnessScorer;
    }

    public async Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var generatedStrategies = _configGenerator.Generate(config.Symbol, config.Bounds, config.SampleSize);
        var qualifiedResults = new ConcurrentBag<(GeneratedStrategy Strategy, BacktestResult Result, decimal Score)>();
        var completed = 0;

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism > 0
                ? config.MaxDegreeOfParallelism
                : Environment.ProcessorCount,
        };

        await Parallel.ForEachAsync(generatedStrategies, parallelOptions, async (generatedStrategy, token) =>
        {
            try
            {
                var backtestSymbol = string.IsNullOrWhiteSpace(config.BacktestSymbol)
                    ? config.Symbol
                    : config.BacktestSymbol;

                var backtestConfig = new BacktestConfig
                {
                    Symbol = backtestSymbol,
                    Intervals = RequiredIntervals,
                    StartDateUtc = config.StartDateUtc,
                    EndDateUtc = config.EndDateUtc,
                    InitialCapital = config.InitialCapital,
                    Strategy = generatedStrategy.Config,
                    Execution = new ExecutionConfig(),
                    EnableAuditLog = false,
                };

                var result = await _backtestRunner.RunAsync(backtestConfig, token);

                if (_fitnessScorer.IsQualified(result, config.Thresholds, config.InitialCapital))
                {
                    qualifiedResults.Add((generatedStrategy, result, _fitnessScorer.Score(result)));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A single failed backtest should not fail the full sweep.
            }
            finally
            {
                var current = Interlocked.Increment(ref completed);
                onProgress?.Invoke(current, generatedStrategies.Count);
            }
        });

        stopwatch.Stop();

        var ranked = qualifiedResults
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Result.TotalPnL)
            .Take(10)
            .Select((item, index) => new RankedResult(index + 1, item.Score, item.Strategy, item.Result))
            .ToList();

        return new SweepResult(ranked, generatedStrategies.Count, qualifiedResults.Count, stopwatch.ElapsedMilliseconds);
    }
}