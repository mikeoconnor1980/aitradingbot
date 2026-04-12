namespace TradingApp.Application.Abstractions.Auth;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken);
}

public sealed record GoogleUserInfo(string Subject, string Email, string Name, string? Picture);
