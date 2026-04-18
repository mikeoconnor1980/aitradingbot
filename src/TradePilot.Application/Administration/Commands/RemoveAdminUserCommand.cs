using MediatR;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Administration.Commands;

public sealed record RemoveAdminUserCommand(Guid GrantId, AppIdentity Identity) : Command;

public sealed class RemoveAdminUserCommandHandler : CommandHandler<RemoveAdminUserCommand>
{
    private readonly IAdminUserGrantRepository _adminUserGrantRepository;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public RemoveAdminUserCommandHandler(
        IAdminUserGrantRepository adminUserGrantRepository,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _adminUserGrantRepository = adminUserGrantRepository;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public override async Task<Unit> Handle(RemoveAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (!await _adminAuthorizationService.IsAdminAsync(request.Identity, cancellationToken))
        {
            throw new UnauthorizedAccessException("Only administrators can manage admin users.");
        }

        var grant = await _adminUserGrantRepository.GetByIdAsync(request.GrantId, cancellationToken);
        if (grant is null)
        {
            throw new NotFoundException(nameof(AdminUserGrant), request.GrantId);
        }

        var adminCount = await _adminUserGrantRepository.CountAsync(cancellationToken);
        if (adminCount <= 1)
        {
            throw new ConflictException("At least one administrator must remain.");
        }

        await _adminUserGrantRepository.RemoveAsync(grant, cancellationToken);
        return Unit.Value;
    }
}