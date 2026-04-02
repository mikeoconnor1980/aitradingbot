using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingApp.Api.Hubs;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Api.Services;

public sealed class BacktestProcessorService : BackgroundService
{
    private readonly BacktestJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MarketDataHub> _hubContext;
    private readonly ILogger<BacktestProcessorService> _logger;

    public BacktestProcessorService(
        BacktestJobQueue queue,
        IServiceScopeFactory scopeFactory,
        IHubContext<MarketDataHub> hubContext,
        ILogger<BacktestProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BacktestProcessorService started");

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

        try
        {
            var result = await runner.RunAsync(config, OnProgress, stoppingToken);
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
                    : null);

            await repository.UpdateAsync(backtestRun, stoppingToken);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backtest {BacktestRunId} failed", job.BacktestRunId);
            backtestRun.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await repository.UpdateAsync(backtestRun, stoppingToken);
            await BroadcastStatusAsync(backtestRun);
        }

        void OnProgress(int candlesProcessed, int totalCandles)
        {
            if (backtestRun.TotalCandles == 0 && totalCandles > 0)
            {
                backtestRun.MarkRunning(totalCandles);
            }

            backtestRun.UpdateProgress(candlesProcessed);

            if (backtestRun.Progress != lastBroadcastPercent)
            {
                lastBroadcastPercent = backtestRun.Progress;
                _ = BroadcastProgressAsync(backtestRun);
            }
        }
    }

    private Task BroadcastProgressAsync(BacktestRun backtestRun)
    {
        return _hubContext.Clients.All.SendAsync("ReceiveBacktestProgress", new
        {
            id = backtestRun.Id,
            status = backtestRun.Status.ToString(),
            progress = backtestRun.Progress,
            totalCandles = backtestRun.TotalCandles,
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
        };
    }
}
