namespace TradingApp.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private User()
    {
    }

    public static User Create(string email, string displayName, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsActive = true,
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
