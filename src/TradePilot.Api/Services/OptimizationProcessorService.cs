using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using TradePilot.Api.Hubs;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Optimization;
using TradePilot.Application.Optimization.Services;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Services;

public sealed class OptimizationProcessorService : BackgroundService
{
    private readonly OptimizationJobQueue _queue;
    private readonly OptimizationCancellationRegistry _cancellationRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly ILogger<OptimizationProcessorService> _logger;

    public OptimizationProcessorService(
        OptimizationJobQueue queue,
        OptimizationCancellationRegistry cancellationRegistry,
        IServiceScopeFactory scopeFactory,
        IHubContext<MarketDataHub> hubContext,
        ILogger<OptimizationProcessorService> logger)
    {
        _queue = queue;
        _cancellationRegistry = cancellationRegistry;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OptimizationProcessorService started");

        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessJobAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Optimization job {OptimizationRunId} failed with unhandled exception", job.OptimizationRunId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        _logger.LogInformation("OptimizationProcessorService stopped");
    }

    private async Task ProcessJobAsync(OptimizationJob job, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOptimizationRunRepository>();
        var sweepRunner = scope.ServiceProvider.GetRequiredService<ISweepRunner>();

        var run = await repository.GetByIdAsync(job.OptimizationRunId, stoppingToken);
        if (run is null)
        {
            _logger.LogWarning("Optimization run {OptimizationRunId} not found, skipping", job.OptimizationRunId);
            return;
        }

        if (run.Status == Domain.Enums.OptimizationStatus.Cancelled)
        {
            _logger.LogInformation("Optimization run {OptimizationRunId} was cancelled before processing, skipping", job.OptimizationRunId);
            await BroadcastProgressAsync(run);
            return;
        }

        using var jobCts = _cancellationRegistry.Register(job.OptimizationRunId);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCts.Token);
        var jobToken = linkedCts.Token;

        run.MarkRunning();
        await repository.UpdateAsync(run, stoppingToken);
        await BroadcastProgressAsync(run);

        var stopwatch = Stopwatch.StartNew();
        var progressLock = new SemaphoreSlim(1, 1);
        var progressTasks = new ConcurrentBag<Task>();

        try
        {
            var result = await sweepRunner.RunAsync(job.Config, OnProgress, jobToken);
            await Task.WhenAll(progressTasks);

            var resultEntities = result.TopResults
                .Select(ranked => OptimizationResult.Create(
                    run.Id,
                    ranked.Rank,
                    ranked.FitnessScore,
                    JsonSerializer.Serialize(ranked.Strategy.Config, StrategyJsonOptions.Default),
                    ranked.Strategy.Description,
                    ranked.BacktestResult.TotalPnL,
                    ranked.BacktestResult.WinRate,
                    ranked.BacktestResult.MaxDrawdownAbsolute,
                    ranked.BacktestResult.TotalTrades,
                    ranked.BacktestResult.WinningTrades,
                    ranked.BacktestResult.LosingTrades,
                    ranked.BacktestResult.TotalFeesPaid,
                    ranked.BacktestResult.AverageTradePnL,
                    ranked.BacktestResult.AverageHoldTime.TotalMinutes,
                    ranked.OutOfSample?.TotalPnl,
                    ranked.OutOfSample?.WinRate,
                    ranked.OutOfSample?.MaxDrawdown,
                    ranked.OutOfSample?.TotalTrades,
                    ranked.OutOfSample?.FitnessScore,
                    ranked.Metrics?.SharpeRatio,
                    ranked.Metrics?.SortinoRatio,
                    ranked.Metrics?.ProfitFactor,
                    ranked.Metrics?.CalmarRatio))
                .ToList();

            await repository.AddResultsAsync(resultEntities, stoppingToken);

            stopwatch.Stop();
            run.MarkCompleted(result.TotalQualified, result.TotalFailed, Math.Max(1, stopwatch.ElapsedMilliseconds));
            await repository.UpdateAsync(run, stoppingToken);
            await BroadcastProgressAsync(run);

            _logger.LogInformation(
                "Optimization {OptimizationRunId} completed in {ElapsedMs}ms with {QualifiedCount} qualified results",
                run.Id,
                result.ElapsedMs,
                result.TotalQualified);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            run.MarkFailed("Optimization was cancelled due to server shutdown.");
            await repository.UpdateAsync(run, CancellationToken.None);
            await BroadcastProgressAsync(run);
            throw;
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            _logger.LogInformation("Optimization {OptimizationRunId} was cancelled by user", run.Id);
            run.MarkCancelled();
            await repository.UpdateAsync(run, CancellationToken.None);
            await BroadcastProgressAsync(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Optimization {OptimizationRunId} failed", run.Id);
            run.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await repository.UpdateAsync(run, CancellationToken.None);
            await BroadcastProgressAsync(run);
        }
        finally
        {
            _cancellationRegistry.Remove(job.OptimizationRunId);
            progressLock.Dispose();
        }

        void OnProgress(SweepProgress progress)
        {
            progressTasks.Add(PersistProgressAsync(progress));
        }

        async Task PersistProgressAsync(SweepProgress progress)
        {
            await progressLock.WaitAsync(jobToken);

            try
            {
                run.UpdateProgress(progress.Completed, progress.Total);
                await repository.UpdateAsync(run, jobToken);
                await BroadcastProgressAsync(run, progress.Phase, progress.EstimatedRemainingMs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist optimization progress for run {OptimizationRunId}", run.Id);
            }
            finally
            {
                progressLock.Release();
            }
        }
    }

    private Task BroadcastProgressAsync(OptimizationRun run, string? phase = null, long? estimatedRemainingMs = null)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveOptimizationProgress", new
        {
            id = run.Id,
            status = run.Status.ToString(),
            completed = run.CompletedCount,
            total = run.TotalCombinations,
            errorMessage = run.ErrorMessage,
            phase,
            estimatedRemainingMs,
        });
    }
}