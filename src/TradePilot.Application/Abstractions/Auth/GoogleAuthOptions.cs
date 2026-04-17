namespace TradePilot.Application.Abstractions.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
}
