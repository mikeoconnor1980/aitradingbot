using System.Text.Json;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Backtesting;

public sealed record RunBacktestCommand(
    string Symbol,
    string[] Intervals,
    DateTime StartDate,
    DateTime EndDate,
    StrategyConfig StrategyConfig,
    ExecutionConfig ExecutionConfig,
    decimal InitialCapital,
    bool EnableAuditLog,
    AppIdentity Identity,
    Guid? StrategyId = null) : Command<BacktestRunResponse>;

public sealed class RunBacktestCommandHandler : CommandHandler<RunBacktestCommand, BacktestRunResponse>
{
    private readonly IBacktestRunRepository _backtestRunRepository;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _strategyRevisionRepository;
    private readonly BacktestJobQueue _backtestJobQueue;

    public RunBacktestCommandHandler(
        IBacktestRunRepository backtestRunRepository,
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository strategyRevisionRepository,
        BacktestJobQueue backtestJobQueue)
    {
        _backtestRunRepository = backtestRunRepository;
        _strategyRepository = strategyRepository;
        _strategyRevisionRepository = strategyRevisionRepository;
        _backtestJobQueue = backtestJobQueue;
    }

    public override async Task<BacktestRunResponse> Handle(RunBacktestCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Intervals);
        ArgumentNullException.ThrowIfNull(request.StrategyConfig);
        ArgumentNullException.ThrowIfNull(request.ExecutionConfig);

        var startDateUtc = request.StartDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc)
            : request.StartDate.ToUniversalTime();
        var endDateUtc = request.EndDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc)
            : request.EndDate.ToUniversalTime();

        int? strategyRevisionId = null;
        string? strategyName = null;

        if (request.StrategyId.HasValue)
        {
            ArgumentNullException.ThrowIfNull(request.Identity);

            var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Strategy), request.StrategyId.Value);

            if (strategy.UserId != request.Identity.UserId || !strategy.IsActive)
            {
                throw new NotFoundException(nameof(Strategy), request.StrategyId.Value);
            }

            strategyName = strategy.Name;

            var latestRevisionNumber = await _strategyRevisionRepository
                .GetLatestRevisionNumberAsync(strategy.Id, cancellationToken);
            strategyRevisionId = latestRevisionNumber > 0 ? latestRevisionNumber : null;
        }

        var strategyConfigJson = BacktestRunResponseMapper.SerializeStrategyConfig(request.StrategyConfig);
        var executionConfigJson = BacktestRunResponseMapper.SerializeExecutionConfig(request.ExecutionConfig);

        var backtestRun = BacktestRun.CreateQueued(
            symbol: request.Symbol,
            intervalsJson: JsonSerializer.Serialize(request.Intervals),
            startDateUtc: new DateTimeOffset(startDateUtc).ToUnixTimeMilliseconds(),
            endDateUtc: new DateTimeOffset(endDateUtc).ToUnixTimeMilliseconds(),
            strategyConfigJson: strategyConfigJson,
            executionConfigJson: executionConfigJson,
            initialCapital: request.InitialCapital,
            auditLogEnabled: request.EnableAuditLog,
            strategyId: request.StrategyId,
            strategyRevisionId: strategyRevisionId);

        await _backtestRunRepository.AddAsync(backtestRun, cancellationToken);
        await _backtestJobQueue.EnqueueAsync(new BacktestJob(backtestRun.Id), cancellationToken);

        return BacktestRunResponseMapper.ToResponse(backtestRun, strategyName);
    }
}