using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Administration.Commands;

public sealed record AddAdminUserCommand(string Email, AppIdentity Identity) : CreateCommand;

public sealed class AddAdminUserCommandHandler : CreateCommandHandler<AddAdminUserCommand>
{
    private readonly IAdminUserGrantRepository _adminUserGrantRepository;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public AddAdminUserCommandHandler(
        IAdminUserGrantRepository adminUserGrantRepository,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _adminUserGrantRepository = adminUserGrantRepository;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public override async Task<Guid> Handle(AddAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (!await _adminAuthorizationService.IsAdminAsync(request.Identity, cancellationToken))
        {
            throw new UnauthorizedAccessException("Only administrators can manage admin users.");
        }

        var normalizedEmail = AdminUserGrant.NormalizeEmail(request.Email);
        if (await _adminUserGrantRepository.ExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("That email already has admin access.");
        }

        var grant = AdminUserGrant.Create(normalizedEmail);
        await _adminUserGrantRepository.AddAsync(grant, cancellationToken);

        return grant.Id;
    }
}