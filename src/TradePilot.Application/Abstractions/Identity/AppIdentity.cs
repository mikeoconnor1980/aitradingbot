namespace TradePilot.Application.Abstractions.Identity;

public sealed class AppIdentity
{
    public string UserId { get; }
    public string Email { get; }

    public AppIdentity(string userId, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        UserId = userId;
        Email = email;
    }

    public static AppIdentity System { get; } = new("system", "system@TradePilot.local");
}