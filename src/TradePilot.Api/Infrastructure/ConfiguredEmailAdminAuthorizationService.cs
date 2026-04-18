using TradePilot.Application.Abstractions.Identity;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Infrastructure;

public sealed class ConfiguredEmailAdminAuthorizationService : IAdminAuthorizationService
{
    private readonly HashSet<string> _emails;

    public ConfiguredEmailAdminAuthorizationService(IConfiguration configuration)
    {
        var emails = configuration
            .GetSection(AdminAuthorizationOptions.SectionName)
            .GetSection("Emails")
            .Get<string[]>()
            ?? [];

        _emails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdmin(AppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return IsAdmin(identity.Email);
    }

    public bool IsAdmin(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && _emails.Contains(email.Trim());
    }
}