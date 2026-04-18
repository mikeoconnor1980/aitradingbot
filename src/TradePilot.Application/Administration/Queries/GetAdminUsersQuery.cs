using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Administration.Models;

namespace TradePilot.Application.Administration.Queries;

public sealed record GetAdminUsersQuery(AppIdentity Identity) : Query<IReadOnlyList<AdminUserDto>>;

public sealed class GetAdminUsersQueryHandler : QueryHandler<GetAdminUsersQuery, IReadOnlyList<AdminUserDto>>
{
    private readonly IAdminUserGrantRepository _adminUserGrantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public GetAdminUsersQueryHandler(
        IAdminUserGrantRepository adminUserGrantRepository,
        IUserRepository userRepository,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _adminUserGrantRepository = adminUserGrantRepository;
        _userRepository = userRepository;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public override async Task<IReadOnlyList<AdminUserDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        if (!await _adminAuthorizationService.IsAdminAsync(request.Identity, cancellationToken))
        {
            throw new UnauthorizedAccessException("Only administrators can manage admin users.");
        }

        var grants = await _adminUserGrantRepository.GetAllAsync(cancellationToken);
        var usersByEmail = await _userRepository.GetByEmailsAsync(grants.Select(grant => grant.Email).ToArray(), cancellationToken);

        return grants
            .Select(grant =>
            {
                usersByEmail.TryGetValue(grant.Email, out var user);

                return new AdminUserDto(
                    grant.Id,
                    grant.Email,
                    user?.Id,
                    user?.DisplayName,
                    user is not null,
                    grant.CreatedAtUtc);
            })
            .OrderBy(admin => admin.Email)
            .ToList();
    }
}