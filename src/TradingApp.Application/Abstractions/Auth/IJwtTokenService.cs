using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Auth;

public interface IJwtTokenService
{
    AuthTokens GenerateTokens(User user);
    (Guid UserId, string Email)? ValidateRefreshToken(string refreshToken);
}
