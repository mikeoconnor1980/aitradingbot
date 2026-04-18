using MediatR;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.StrategyAuthoring.Commands;

public sealed record UnpublishStrategyTemplateCommand(Guid TemplateId, AppIdentity Identity) : Command;

public sealed class UnpublishStrategyTemplateCommandHandler : CommandHandler<UnpublishStrategyTemplateCommand>
{
    private readonly IStrategyTemplateRepository _templateRepository;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public UnpublishStrategyTemplateCommandHandler(
        IStrategyTemplateRepository templateRepository,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _templateRepository = templateRepository;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public override async Task<Unit> Handle(UnpublishStrategyTemplateCommand request, CancellationToken cancellationToken)
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
            throw new ConflictException("Built-in strategy templates cannot be unpublished.");
        }

        template.Deactivate();
        await _templateRepository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}