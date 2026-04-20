using Microsoft.Extensions.DependencyInjection;
using Moq;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class AgentExchangeCredentialAccessorTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string WalletAddress = "0xb63a3948477254cc17E0fb444050B9E161FCcFA3";

    [TestMethod]
    public async Task GivenWalletMappedAgentAndActiveCredential_WhenResolvingBinanceCredential_ThenReturnsDecryptedCredential()
    {
        var walletRepository = new Mock<IUserWalletAddressRepository>();
        var credentialRepository = new Mock<IUserExchangeCredentialRepository>();
        var encryptionService = new Mock<ICredentialEncryptionService>();
        var signerProvider = new Mock<ISignerProvider>();

        signerProvider.SetupGet(provider => provider.IsConfigured).Returns(true);
        signerProvider.SetupGet(provider => provider.WalletAddress).Returns(WalletAddress);

        walletRepository
            .Setup(repository => repository.GetActiveByWalletAddressAsync(WalletAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(TestUserId, WalletAddress));

        credentialRepository
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserExchangeCredential.Create(TestUserId, Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance"));

        encryptionService
            .Setup(service => service.Decrypt("encrypted-secret"))
            .Returns("decrypted-secret");

        var services = new ServiceCollection();
        services.AddScoped(_ => walletRepository.Object);
        services.AddScoped(_ => credentialRepository.Object);
        services.AddScoped(_ => encryptionService.Object);

        using var provider = services.BuildServiceProvider();

        var accessor = new AgentExchangeCredentialAccessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            signerProvider.Object);

        var result = await accessor.GetActiveCredentialAsync(Exchange.Binance);

        result.Should().NotBeNull();
        result!.ApiKey.Should().Be("api-key");
        result.ApiSecret.Should().Be("decrypted-secret");
        result.Exchange.Should().Be(Exchange.Binance);
    }

    [TestMethod]
    public async Task GivenUnconfiguredSigner_WhenResolvingCredential_ThenReturnsNull()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var signerProvider = new Mock<ISignerProvider>();
        signerProvider.SetupGet(provider => provider.IsConfigured).Returns(false);

        var accessor = new AgentExchangeCredentialAccessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            signerProvider.Object);

        var result = await accessor.GetActiveCredentialAsync(Exchange.Binance);

        result.Should().BeNull();
    }
}