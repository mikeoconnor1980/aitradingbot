using MediatR;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Optimization;

public sealed record CancelOptimizationCommand(Guid Id) : Command;

public sealed class CancelOptimizationCommandHandler : CommandHandler<CancelOptimizationCommand>
{
    private readonly IOptimizationRunRepository _repository;
    private readonly OptimizationCancellationRegistry _cancellationRegistry;

    public CancelOptimizationCommandHandler(
        IOptimizationRunRepository repository,
        OptimizationCancellationRegistry cancellationRegistry)
    {
        _repository = repository;
        _cancellationRegistry = cancellationRegistry;
    }

    public override async Task<Unit> Handle(CancelOptimizationCommand request, CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.OptimizationRun), request.Id);

        if (run.Status is not (OptimizationStatus.Queued or OptimizationStatus.Running))
        {
            return Unit.Value;
        }

        if (run.Status == OptimizationStatus.Running)
        {
            _cancellationRegistry.TryCancel(request.Id);
        }
        else
        {
            run.MarkCancelled();
            await _repository.UpdateAsync(run, cancellationToken);
        }

        return Unit.Value;
    }
}
