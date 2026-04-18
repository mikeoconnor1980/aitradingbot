using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Infrastructure;

public sealed class DbAdminAuthorizationService : IAdminAuthorizationService
{
    private readonly IAdminUserGrantRepository _adminUserGrantRepository;

    public DbAdminAuthorizationService(IAdminUserGrantRepository adminUserGrantRepository)
    {
        _adminUserGrantRepository = adminUserGrantRepository;
    }

    public Task<bool> IsAdminAsync(AppIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return IsAdminAsync(identity.Email, cancellationToken);
    }

    public async Task<bool> IsAdminAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return await _adminUserGrantRepository.ExistsAsync(email, cancellationToken);
    }
}