using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using TradingApp.Api.Hubs;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Optimization;
using TradingApp.Application.Optimization.Services;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Entities;

namespace TradingApp.Api.Services;

public sealed class OptimizationProcessorService : BackgroundService
{
    private readonly OptimizationJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly ILogger<OptimizationProcessorService> _logger;

    public OptimizationProcessorService(
        OptimizationJobQueue queue,
        IServiceScopeFactory scopeFactory,
        IHubContext<MarketDataHub> hubContext,
        ILogger<OptimizationProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OptimizationProcessorService started");

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

        run.MarkRunning();
        await repository.UpdateAsync(run, stoppingToken);
        await BroadcastProgressAsync(run);

        var stopwatch = Stopwatch.StartNew();
        var progressLock = new SemaphoreSlim(1, 1);
        var progressTasks = new ConcurrentBag<Task>();

        try
        {
            var result = await sweepRunner.RunAsync(job.Config, OnProgress, stoppingToken);
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
                    ranked.BacktestResult.AverageHoldTime.TotalMinutes))
                .ToList();

            await repository.AddResultsAsync(resultEntities, stoppingToken);

            stopwatch.Stop();
            run.MarkCompleted(result.TotalQualified, Math.Max(1, stopwatch.ElapsedMilliseconds));
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Optimization {OptimizationRunId} failed", run.Id);
            run.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await repository.UpdateAsync(run, CancellationToken.None);
            await BroadcastProgressAsync(run);
        }
        finally
        {
            progressLock.Dispose();
        }

        void OnProgress(int completed, int total)
        {
            progressTasks.Add(PersistProgressAsync(completed, total));
        }

        async Task PersistProgressAsync(int completed, int total)
        {
            await progressLock.WaitAsync(stoppingToken);

            try
            {
                run.UpdateProgress(completed, total);
                await repository.UpdateAsync(run, stoppingToken);
                await BroadcastProgressAsync(run);
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

    private Task BroadcastProgressAsync(OptimizationRun run)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveOptimizationProgress", new
        {
            id = run.Id,
            status = run.Status.ToString(),
            completed = run.CompletedCount,
            total = run.TotalCombinations,
            errorMessage = run.ErrorMessage,
        });
    }
}