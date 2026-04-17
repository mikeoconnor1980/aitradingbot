using System.Text.Json;
using MediatR;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record RestoreStrategyVersionCommand(
    Guid StrategyId,
    int RevisionNumber,
    AppIdentity Identity) : Command;

public sealed class RestoreStrategyVersionCommandHandler
    : CommandHandler<RestoreStrategyVersionCommand>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IChangeSummaryGenerator _changeSummaryGenerator;

    public RestoreStrategyVersionCommandHandler(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IChangeSummaryGenerator changeSummaryGenerator)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _changeSummaryGenerator = changeSummaryGenerator;
    }

    public override async Task<Unit> Handle(
        RestoreStrategyVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.RevisionNumber, 1);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        if (strategy.IsRunning)
        {
            throw new ConflictException("Pause the strategy before restoring a revision");
        }

        var revision = await _revisionRepository.GetByStrategyAndRevisionAsync(
            request.StrategyId,
            request.RevisionNumber,
            cancellationToken);

        if (revision is null)
        {
            throw new NotFoundException(nameof(StrategyRevision), request.RevisionNumber);
        }

        var restoredConfig = JsonSerializer.Deserialize<StrategyConfig>(
            revision.ConfigJson,
            StrategyJsonOptions.Default)
            ?? throw new DomainException("Unable to restore strategy revision");

        var nameExists = await _strategyRepository.ExistsWithNameAsync(
            request.Identity.UserId,
            restoredConfig.StrategyName,
            request.StrategyId,
            cancellationToken);

        if (nameExists)
        {
            throw new DuplicateStrategyNameException(restoredConfig.StrategyName);
        }

        var previousConfigJson = strategy.ConfigJson;

        strategy.Update(restoredConfig.StrategyName, revision.ConfigJson);
        await _strategyRepository.UpdateAsync(strategy, cancellationToken);

        var restoreRevision = StrategyRevision.Create(
            strategy.Id,
            strategy.Version,
            revision.ConfigJson,
            RevisionSource.Restore,
            _changeSummaryGenerator.Generate(previousConfigJson, revision.ConfigJson),
            $"Restored from revision {request.RevisionNumber}");

        await _revisionRepository.AddAsync(restoreRevision, cancellationToken);

        return Unit.Value;
    }
}