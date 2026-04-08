using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TradingApp.Application.Abstractions.Auth;
using TradingApp.Domain.Entities;

namespace TradingApp.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _refreshValidationParameters;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _refreshValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    }

    public AuthTokens GenerateTokens(User user)
    {
        var now = DateTime.UtcNow;
        var accessExpiry = now.AddMinutes(_options.AccessTokenExpiryMinutes);
        var refreshExpiry = now.AddDays(_options.RefreshTokenExpiryDays);

        var accessToken = GenerateToken(user, accessExpiry, "access");
        var refreshToken = GenerateToken(user, refreshExpiry, "refresh");

        return new AuthTokens
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = new DateTimeOffset(accessExpiry).ToUnixTimeMilliseconds(),
        };
    }

    public (Guid UserId, string Email)? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(refreshToken, _refreshValidationParameters, out var validatedToken);

            var tokenTypeClaim = principal.FindFirst("token_type")?.Value;
            if (tokenTypeClaim != "refresh")
            {
                return null;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;

            if (userIdClaim is null || emailClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return (userId, emailClaim);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private string GenerateToken(User user, DateTime expiry, string tokenType)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("token_type", tokenType),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiry,
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
