using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Auth;

public interface IJwtTokenService
{
    AuthTokens GenerateTokens(User user);
    (Guid UserId, string Email)? ValidateRefreshToken(string refreshToken);
}
