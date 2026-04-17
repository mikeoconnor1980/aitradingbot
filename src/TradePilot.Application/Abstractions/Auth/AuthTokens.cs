namespace TradePilot.Application.Abstractions.Auth;

public sealed class AuthTokens
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required long ExpiresAtUtc { get; init; }
}
