using TradePilot.Application.Abstractions.Identity;

namespace TradePilot.Application.Abstractions.Services;

public interface IAdminAuthorizationService
{
    Task<bool> IsAdminAsync(AppIdentity identity, CancellationToken cancellationToken = default);
    Task<bool> IsAdminAsync(string email, CancellationToken cancellationToken = default);
}