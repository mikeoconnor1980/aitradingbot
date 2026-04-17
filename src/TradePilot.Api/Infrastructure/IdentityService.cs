using System.Security.Claims;
using TradePilot.Application.Abstractions.Identity;

namespace TradePilot.Api.Infrastructure;

public sealed class IdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AppIdentity Identity
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;

                if (userId is not null && email is not null)
                {
                    return new AppIdentity(userId, email);
                }
            }

            // Fallback for dev/unauthenticated scenarios — will be blocked by [Authorize]
            return new AppIdentity("dev-user", "developer@TradePilot.local");
        }
    }
}