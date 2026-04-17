using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradePilot.Api.Hubs;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Api.Services;

public sealed class BacktestProcessorService : BackgroundService
{
    private readonly BacktestJobQueue _queue;
    private readonly BacktestCancellationManager _cancellationManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly ILogger<BacktestProcessorService> _logger;

    public BacktestProcessorService(
        BacktestJobQueue queue,
        BacktestCancellationManager cancellationManager,
        IServiceScopeFactory scopeFactory,
        IHubContext<MarketDataHub> hubContext,
        ILogger<BacktestProcessorService> logger)
    {
        _queue = queue;
        _cancellationManager = cancellationManager;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BacktestProcessorService started");

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
                    _logger.LogError(ex, "Backtest job {BacktestRunId} failed with unhandled exception", job.BacktestRunId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        _logger.LogInformation("BacktestProcessorService stopped");
    }

    private async Task ProcessJobAsync(BacktestJob job, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBacktestRunRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<IBacktestRunner>();

        var backtestRun = await repository.GetByIdAsync(job.BacktestRunId, stoppingToken);
        if (backtestRun is null)
        {
            _logger.LogWarning("Backtest run {BacktestRunId} not found, skipping", job.BacktestRunId);
            return;
        }

        var config = BuildConfig(backtestRun);
        var stopwatch = Stopwatch.StartNew();
        var lastBroadcastPercent = -1;
        long lastCandleTimestamp = 0;

        using var jobCts = _cancellationManager.Register(job.BacktestRunId);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCts.Token);
        var linkedToken = linkedCts.Token;

        try
        {
            var result = await runner.RunAsync(config, OnProgress, linkedToken);
            stopwatch.Stop();

            backtestRun.MarkCompleted(
                candlesReplayed: result.CandlesReplayed,
                elapsedMs: Math.Max(1, stopwatch.ElapsedMilliseconds),
                totalTrades: result.TotalTrades,
                winningTrades: result.WinningTrades,
                losingTrades: result.LosingTrades,
                winRate: result.WinRate,
                totalPnl: result.TotalPnL,
                maxDrawdown: result.MaxDrawdownAbsolute,
                averageTradePnl: result.AverageTradePnL,
                averageHoldTimeMinutes: result.AverageHoldTime.TotalMinutes,
                hedgesOpened: result.HedgesOpened,
                totalFeesPaid: result.TotalFeesPaid,
                tradesJson: BacktestRunResponseMapper.SerializeTrades(result.TradeLog),
                equityTimeSeriesJson: BacktestRunResponseMapper.SerializeEquityTimeSeries(result.EquityTimeSeries),
                candleLogJson: result.CandleEvaluationLog is not null
                    ? BacktestRunResponseMapper.SerializeCandleLog(result.CandleEvaluationLog)
                    : null,
                orderEventLogJson: result.OrderEventLog is not null
                    ? BacktestRunResponseMapper.SerializeOrderEventLog(result.OrderEventLog)
                    : null,
                gridCycleLogJson: result.GridCycleLog is not null
                    ? BacktestRunResponseMapper.SerializeGridCycleLog(result.GridCycleLog)
                    : null,
                expectancy: result.Expectancy,
                profitFactor: result.ProfitFactor,
                sqn: result.Sqn,
                kellyPercent: result.KellyPercent,
                halfKellyPercent: result.HalfKellyPercent,
                winLossRRatio: result.WinLossRRatio);

            await repository.UpdateAsync(backtestRun, CancellationToken.None);
            await BroadcastStatusAsync(backtestRun);

            _logger.LogInformation(
                "Backtest {BacktestRunId} completed in {ElapsedMs}ms — {TotalTrades} trades, PnL={TotalPnl}",
                job.BacktestRunId, stopwatch.ElapsedMilliseconds, result.TotalTrades, result.TotalPnL);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            backtestRun.MarkFailed("Backtest was cancelled due to server shutdown.");
            await repository.UpdateAsync(backtestRun, CancellationToken.None);
            await BroadcastStatusAsync(backtestRun);
            throw;
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            _logger.LogInformation("Backtest {BacktestRunId} cancelled by user", job.BacktestRunId);
            await repository.DeleteAsync(job.BacktestRunId, CancellationToken.None);
            await BroadcastCancelledAsync(job.BacktestRunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backtest {BacktestRunId} failed", job.BacktestRunId);
            backtestRun.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await repository.UpdateAsync(backtestRun, CancellationToken.None);
            await BroadcastStatusAsync(backtestRun);
        }
        finally
        {
            _cancellationManager.Remove(job.BacktestRunId);
        }

        void OnProgress(int candlesProcessed, int totalCandles, long currentTimestamp)
        {
            if (backtestRun.TotalCandles == 0 && totalCandles > 0)
            {
                backtestRun.MarkRunning(totalCandles);
            }

            backtestRun.UpdateProgress(candlesProcessed);
            lastCandleTimestamp = currentTimestamp;

            if (backtestRun.Progress != lastBroadcastPercent)
            {
                lastBroadcastPercent = backtestRun.Progress;
                _ = BroadcastProgressAsync(backtestRun, lastCandleTimestamp)
                    .ContinueWith(
                        t => _logger.LogWarning(t.Exception, "Failed to broadcast backtest progress for {BacktestRunId}", backtestRun.Id),
                        TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    private Task BroadcastProgressAsync(BacktestRun backtestRun, long currentTimestamp)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveBacktestProgress", new
        {
            id = backtestRun.Id,
            status = backtestRun.Status.ToString(),
            progress = backtestRun.Progress,
            totalCandles = backtestRun.TotalCandles,
            currentTimestamp,
        });
    }

    private Task BroadcastStatusAsync(BacktestRun backtestRun)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveBacktestProgress", new
        {
            id = backtestRun.Id,
            status = backtestRun.Status.ToString(),
            progress = backtestRun.Progress,
            totalCandles = backtestRun.TotalCandles,
            errorMessage = backtestRun.ErrorMessage,
        });
    }

    private Task BroadcastCancelledAsync(Guid backtestRunId)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveBacktestProgress", new
        {
            id = backtestRunId,
            status = "Cancelled",
            progress = 0,
            totalCandles = 0,
        });
    }

    private static BacktestConfig BuildConfig(BacktestRun run)
    {
        var strategyConfig = JsonSerializer.Deserialize<StrategyConfig>(
            run.StrategyConfigJson,
            StrategyJsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize strategy config.");

        var executionConfig = JsonSerializer.Deserialize<ExecutionConfig>(
            run.ExecutionConfigJson,
            StrategyJsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize execution config.");

        return new BacktestConfig
        {
            Symbol = run.Symbol,
            Intervals = JsonSerializer.Deserialize<string[]>(
                run.IntervalsJson,
                StrategyJsonOptions.Default)
                ?? throw new InvalidOperationException("Failed to deserialize intervals."),
            StartDateUtc = run.StartDateUtc,
            EndDateUtc = run.EndDateUtc,
            InitialCapital = run.InitialCapital,
            Strategy = strategyConfig,
            Execution = executionConfig,
            EnableAuditLog = run.AuditLogEnabled,
            TriggerTimeframe = strategyConfig.Timeframe,
        };
    }
}
