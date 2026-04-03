using System.Text.Json;
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

public sealed record CreateStrategyCommand(StrategyConfig Config, AppIdentity Identity) : CreateCommand;

public sealed class CreateStrategyCommandHandler : CreateCommandHandler<CreateStrategyCommand>
{
    private readonly IStrategyRepository _repository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IChangeSummaryGenerator _changeSummaryGenerator;
    private readonly IStrategyValidator _validator;

    public CreateStrategyCommandHandler(
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

    public override async Task<Guid> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Config);
        ArgumentNullException.ThrowIfNull(request.Identity);

        var validationResult = _validator.Validate(request.Config);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First();
            throw new DomainException(firstError.Message);
        }

        var nameExists = await _repository.ExistsWithNameAsync(
            request.Identity.UserId,
            request.Config.StrategyName,
            cancellationToken: cancellationToken);

        if (nameExists)
        {
            throw new DuplicateStrategyNameException(request.Config.StrategyName);
        }

        var configJson = JsonSerializer.Serialize(request.Config, StrategyJsonOptions.Default);
        var strategy = Strategy.Create(
            request.Identity.UserId,
            request.Config.StrategyName,
            "GridStrategy",
            configJson);

        await _repository.AddAsync(strategy, cancellationToken);

        var revision = StrategyRevision.Create(
            strategy.Id,
            strategy.Version,
            configJson,
            RevisionSourceMapper.MapFrom(request.Config.Source?.EntryPoint),
            _changeSummaryGenerator.Generate(null, configJson));

        await _revisionRepository.AddAsync(revision, cancellationToken);

        return strategy.Id;
    }
}