using Microsoft.AspNetCore.Identity;
using TradePilot.Application.Abstractions.Auth;

namespace TradePilot.Infrastructure.Services;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<string> _hasher = new();

    public string Hash(string password)
    {
        return _hasher.HashPassword(string.Empty, password);
    }

    public bool Verify(string password, string hash)
    {
        var result = _hasher.VerifyHashedPassword(string.Empty, hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
