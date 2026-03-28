using System.Diagnostics;
using System.Text.Json;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting;

public sealed record RunBacktestCommand(
    string Symbol,
    string[] Intervals,
    DateTime StartDate,
    DateTime EndDate,
    GridStrategyConfig StrategyConfig,
    decimal InitialCapital) : Command<BacktestRunResponse>;

public sealed class RunBacktestCommandHandler : CommandHandler<RunBacktestCommand, BacktestRunResponse>
{
    private static readonly TimeSpan ServerTimeout = TimeSpan.FromMinutes(5);

    private readonly IBacktestRunner _backtestRunner;
    private readonly IBacktestRunRepository _backtestRunRepository;

    public RunBacktestCommandHandler(
        IBacktestRunner backtestRunner,
        IBacktestRunRepository backtestRunRepository)
    {
        _backtestRunner = backtestRunner;
        _backtestRunRepository = backtestRunRepository;
    }

    public override async Task<BacktestRunResponse> Handle(RunBacktestCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Intervals);
        ArgumentNullException.ThrowIfNull(request.StrategyConfig);

        var startDateUtc = request.StartDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc)
            : request.StartDate.ToUniversalTime();
        var endDateUtc = request.EndDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc)
            : request.EndDate.ToUniversalTime();
        var strategyConfigJson = BacktestRunResponseMapper.SerializeStrategyConfig(request.StrategyConfig);

        var config = new BacktestConfig
        {
            Symbol = request.Symbol,
            Intervals = request.Intervals,
            StartDateUtc = new DateTimeOffset(startDateUtc).ToUnixTimeMilliseconds(),
            EndDateUtc = new DateTimeOffset(endDateUtc).ToUnixTimeMilliseconds(),
            InitialCapital = request.InitialCapital,
            FeeModel = new FeeModel
            {
                MakerFeeRate = request.StrategyConfig.MakerFee,
                TakerFeeRate = request.StrategyConfig.TakerFee,
                SlippageRate = request.StrategyConfig.Slippage
            },
            StrategyConfigJson = strategyConfigJson
        };

        using var timeoutCts = new CancellationTokenSource(ServerTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();
        var result = await _backtestRunner.RunAsync(config, linkedCts.Token);
        stopwatch.Stop();

        var backtestRun = BacktestRun.Create(
            symbol: request.Symbol,
            intervalsJson: JsonSerializer.Serialize(request.Intervals),
            startDateUtc: config.StartDateUtc,
            endDateUtc: config.EndDateUtc,
            strategyConfigJson: strategyConfigJson,
            initialCapital: request.InitialCapital,
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
            tradesJson: BacktestRunResponseMapper.SerializeTrades(result.TradeLog));

        await _backtestRunRepository.AddAsync(backtestRun, linkedCts.Token);

        return BacktestRunResponseMapper.ToResponse(backtestRun);
    }
}