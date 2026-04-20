namespace TradePilot.Application.Abstractions.Services;

public interface ICredentialEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}