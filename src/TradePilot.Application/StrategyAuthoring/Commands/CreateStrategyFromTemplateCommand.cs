using System.Text.Json;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.StrategyAuthoring.Validation;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record CreateStrategyFromTemplateCommand(Guid TemplateId, AppIdentity Identity) : CreateCommand;

public sealed class CreateStrategyFromTemplateCommandHandler : CreateCommandHandler<CreateStrategyFromTemplateCommand>
{
    private readonly IStrategyTemplateRepository _templateRepository;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IChangeSummaryGenerator _changeSummaryGenerator;
    private readonly IStrategyValidator _validator;
    private readonly StrategyTierConstraintValidator _strategyTierConstraintValidator;

    public CreateStrategyFromTemplateCommandHandler(
        IStrategyTemplateRepository templateRepository,
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IChangeSummaryGenerator changeSummaryGenerator,
        IStrategyValidator validator,
        StrategyTierConstraintValidator strategyTierConstraintValidator)
    {
        _templateRepository = templateRepository;
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _changeSummaryGenerator = changeSummaryGenerator;
        _validator = validator;
        _strategyTierConstraintValidator = strategyTierConstraintValidator;
    }

    public override async Task<Guid> Handle(
        CreateStrategyFromTemplateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || !template.IsActive)
        {
            throw new NotFoundException(nameof(StrategyTemplate), request.TemplateId);
        }

        var config = JsonSerializer.Deserialize<StrategyConfig>(template.ConfigJson, StrategyJsonOptions.Default)
            ?? throw new DomainException("Template configuration is invalid.");

        var validationResult = _validator.Validate(config);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First();
            throw new DomainException(firstError.Message);
        }

        await _strategyTierConstraintValidator.ValidateAsync(
            request.Identity,
            config,
            template.IsBeginnerVisible,
            cancellationToken);

        // Ensure unique name for this user — append a suffix if needed
        var baseName = config.StrategyName;
        var candidateName = baseName;
        var attempt = 1;

        while (await _strategyRepository.ExistsWithNameAsync(
            request.Identity.UserId, candidateName, cancellationToken: cancellationToken))
        {
            attempt++;
            candidateName = $"{baseName} ({attempt})";
        }

        var clonedConfig = config with { StrategyName = candidateName };
        var configJson = JsonSerializer.Serialize(clonedConfig, StrategyJsonOptions.Default);

        var strategy = Strategy.Create(
            request.Identity.UserId,
            candidateName,
            template.StrategyMode switch
            {
                "grid" => "GridStrategy",
                "dca" => "DcaStrategy",
                _ => "SignalStrategy",
            },
            configJson);

        await _strategyRepository.AddAsync(strategy, cancellationToken);

        var revision = StrategyRevision.Create(
            strategy.Id,
            strategy.Version,
            configJson,
            RevisionSource.Template,
            _changeSummaryGenerator.Generate(null, configJson));

        await _revisionRepository.AddAsync(revision, cancellationToken);

        return strategy.Id;
    }
}
