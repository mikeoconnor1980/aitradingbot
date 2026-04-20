using Microsoft.AspNetCore.DataProtection;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Security;

public sealed class DataProtectionCredentialEncryptionService : ICredentialEncryptionService
{
    private readonly IDataProtector _protector;

    public DataProtectionCredentialEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("BinanceApiSecret");
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);
        return _protector.Unprotect(ciphertext);
    }
}