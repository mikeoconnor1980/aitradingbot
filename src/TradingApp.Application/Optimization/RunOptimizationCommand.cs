using System.Text.Json;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Optimization.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Optimization;

public sealed record RunOptimizationCommand(SweepConfig Config) : Command<OptimizationRunResponse>;

public sealed class RunOptimizationCommandHandler : CommandHandler<RunOptimizationCommand, OptimizationRunResponse>
{
    private readonly IOptimizationRunRepository _repository;
    private readonly OptimizationJobQueue _jobQueue;

    public RunOptimizationCommandHandler(IOptimizationRunRepository repository, OptimizationJobQueue jobQueue)
    {
        _repository = repository;
        _jobQueue = jobQueue;
    }

    public override async Task<OptimizationRunResponse> Handle(RunOptimizationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Config);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Config.Symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Config.InitialCapital);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(request.Config.StartDateUtc, request.Config.EndDateUtc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Config.SampleSize);

        var sweepConfigJson = JsonSerializer.Serialize(request.Config);
        var thresholdsJson = JsonSerializer.Serialize(request.Config.Thresholds);

        var run = OptimizationRun.CreateQueued(
            symbol: request.Config.Symbol,
            startDateUtc: request.Config.StartDateUtc,
            endDateUtc: request.Config.EndDateUtc,
            initialCapital: request.Config.InitialCapital,
            sweepConfigJson: sweepConfigJson,
            thresholdsJson: thresholdsJson,
            totalCombinations: request.Config.SampleSize);

        await _repository.AddAsync(run, cancellationToken);
        await _jobQueue.EnqueueAsync(new OptimizationJob(run.Id, request.Config), cancellationToken);

        return OptimizationRunResponseMapper.ToResponse(run);
    }
}