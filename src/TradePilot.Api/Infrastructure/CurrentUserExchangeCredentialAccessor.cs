using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Infrastructure;

public sealed class CurrentUserExchangeCredentialAccessor : IExchangeCredentialAccessor
{
    private readonly IdentityService _identityService;
    private readonly IUserExchangeCredentialRepository _credentialRepository;
    private readonly ICredentialEncryptionService _credentialEncryptionService;

    public CurrentUserExchangeCredentialAccessor(
        IdentityService identityService,
        IUserExchangeCredentialRepository credentialRepository,
        ICredentialEncryptionService credentialEncryptionService)
    {
        _identityService = identityService;
        _credentialRepository = credentialRepository;
        _credentialEncryptionService = credentialEncryptionService;
    }

    public async Task<ExchangeCredentialSnapshot?> GetActiveCredentialAsync(
        Exchange exchange,
        CancellationToken cancellationToken = default)
    {
        if (exchange == Exchange.Hyperliquid)
        {
            return null;
        }

        var identity = _identityService.Identity;
        if (!Guid.TryParse(identity.UserId, out var userId))
        {
            return null;
        }

        var credential = await _credentialRepository.GetActiveByUserIdAndExchangeAsync(userId, exchange, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        return new ExchangeCredentialSnapshot(
            credential.Exchange,
            credential.ApiKey,
            _credentialEncryptionService.Decrypt(credential.EncryptedApiSecret),
            credential.Label);
    }
}