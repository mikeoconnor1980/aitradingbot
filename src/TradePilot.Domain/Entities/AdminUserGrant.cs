using System.Net.Mail;

namespace TradePilot.Domain.Entities;

public sealed class AdminUserGrant
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }

    private AdminUserGrant()
    {
    }

    public static AdminUserGrant Create(string email)
    {
        return new AdminUserGrant
        {
            Id = Guid.NewGuid(),
            Email = NormalizeEmail(email),
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address.Trim().ToLowerInvariant();
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("A valid admin email address is required.", nameof(email), ex);
        }
    }
}