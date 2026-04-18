using MediatR;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record RenameStrategyTemplateCommand(
    Guid TemplateId,
    string Name,
    string Description,
    AppIdentity Identity) : Command;

public sealed class RenameStrategyTemplateCommandHandler : CommandHandler<RenameStrategyTemplateCommand>
{
    private readonly IStrategyTemplateRepository _templateRepository;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public RenameStrategyTemplateCommandHandler(
        IStrategyTemplateRepository templateRepository,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _templateRepository = templateRepository;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public override async Task<Unit> Handle(RenameStrategyTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Identity);

        if (!_adminAuthorizationService.IsAdmin(request.Identity))
        {
            throw new UnauthorizedAccessException("Only administrators can manage strategy library templates.");
        }

        var template = await _templateRepository.GetByIdForUpdateAsync(request.TemplateId, cancellationToken);
        if (template is null || !template.IsActive)
        {
            throw new NotFoundException(nameof(StrategyTemplate), request.TemplateId);
        }

        if (template.IsSystemTemplate)
        {
            throw new ConflictException("Built-in strategy templates cannot be renamed.");
        }

        var name = NormalizeRequiredText(request.Name, 100, "Template name");
        var description = NormalizeRequiredText(request.Description, 500, "Template description");

        if (!string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)
            && await _templateRepository.ExistsWithNameAsync(name, cancellationToken))
        {
            throw new DuplicateStrategyTemplateNameException(name);
        }

        template.Update(
            name,
            description,
            template.StrategyMode,
            template.Direction,
            template.Market,
            template.TagsJson,
            template.ConfigJson,
            template.SortOrder,
            template.IsSystemTemplate);

        await _templateRepository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static string NormalizeRequiredText(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }
}