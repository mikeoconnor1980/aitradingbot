using TradePilot.Application.Abstractions.Identity;

namespace TradePilot.Application.Abstractions.Services;

public interface IAdminAuthorizationService
{
    bool IsAdmin(AppIdentity identity);
    bool IsAdmin(string email);
}