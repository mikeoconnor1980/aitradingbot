using Microsoft.Extensions.DependencyInjection;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Worker.Services;

public sealed class AgentExchangeCredentialAccessor : IExchangeCredentialAccessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISignerProvider _signerProvider;

    public AgentExchangeCredentialAccessor(
        IServiceScopeFactory scopeFactory,
        ISignerProvider signerProvider)
    {
        _scopeFactory = scopeFactory;
        _signerProvider = signerProvider;
    }

    public async Task<ExchangeCredentialSnapshot?> GetActiveCredentialAsync(
        Exchange exchange,
        CancellationToken cancellationToken = default)
    {
        if (exchange == Exchange.Hyperliquid || !_signerProvider.IsConfigured)
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var walletRepository = scope.ServiceProvider.GetRequiredService<IUserWalletAddressRepository>();
        var credentialRepository = scope.ServiceProvider.GetRequiredService<IUserExchangeCredentialRepository>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();

        var wallet = await walletRepository.GetActiveByWalletAddressAsync(
            _signerProvider.WalletAddress,
            cancellationToken);

        if (wallet is null)
        {
            return null;
        }

        var credential = await credentialRepository.GetActiveByUserIdAndExchangeAsync(
            wallet.UserId,
            exchange,
            cancellationToken);

        if (credential is null)
        {
            return null;
        }

        return new ExchangeCredentialSnapshot(
            credential.Exchange,
            credential.ApiKey,
            encryptionService.Decrypt(credential.EncryptedApiSecret),
            credential.Label);
    }
}