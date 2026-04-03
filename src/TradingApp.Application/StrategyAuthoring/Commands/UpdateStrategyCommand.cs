using System.Text.Json;
using MediatR;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Identity;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.StrategyAuthoring.Validation;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.StrategyAuthoring.Commands;

public sealed record UpdateStrategyCommand(Guid Id, StrategyConfig Config, AppIdentity Identity) : Command;

public sealed class UpdateStrategyCommandHandler : CommandHandler<UpdateStrategyCommand>
{
    private readonly IStrategyRepository _repository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IChangeSummaryGenerator _changeSummaryGenerator;
    private readonly IStrategyValidator _validator;

    public UpdateStrategyCommandHandler(
        IStrategyRepository repository,
        IStrategyRevisionRepository revisionRepository,
        IChangeSummaryGenerator changeSummaryGenerator,
        IStrategyValidator validator)
    {
        _repository = repository;
        _revisionRepository = revisionRepository;
        _changeSummaryGenerator = changeSummaryGenerator;
        _validator = validator;
    }

    public override async Task<Unit> Handle(UpdateStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Config);
        ArgumentNullException.ThrowIfNull(request.Identity);

        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (strategy is null || strategy.UserId != request.Identity.UserId || !strategy.IsActive)
        {
            throw new NotFoundException(nameof(Strategy), request.Id);
        }

        var validationResult = _validator.Validate(request.Config);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First();
            throw new DomainException(firstError.Message);
        }

        var nameExists = await _repository.ExistsWithNameAsync(
            request.Identity.UserId,
            request.Config.StrategyName,
            request.Id,
            cancellationToken);

        if (nameExists)
        {
            throw new DuplicateStrategyNameException(request.Config.StrategyName);
        }

        var previousConfigJson = strategy.ConfigJson;
        var configJson = JsonSerializer.Serialize(request.Config, StrategyJsonOptions.Default);
        strategy.Update(request.Config.StrategyName, configJson);

        await _repository.UpdateAsync(strategy, cancellationToken);

        var revision = StrategyRevision.Create(
            strategy.Id,
            strategy.Version,
            configJson,
            RevisionSourceMapper.MapFrom(request.Config.Source?.EntryPoint),
            _changeSummaryGenerator.Generate(previousConfigJson, configJson));

        await _revisionRepository.AddAsync(revision, cancellationToken);

        return Unit.Value;
    }
}