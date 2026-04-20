using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Infrastructure;

[TestClass]
public sealed class CurrentUserExchangeCredentialAccessorTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [TestMethod]
    public async Task GivenConcurrentBinanceLookups_WhenResolvingCredential_ThenRepositoryIsQueriedOncePerRequest()
    {
        var services = new ServiceCollection();
        var repository = new Mock<IUserExchangeCredentialRepository>();
        var encryption = new Mock<ICredentialEncryptionService>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(repo => repo.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await gate.Task;
                return UserExchangeCredential.Create(TestUserId, Exchange.Binance, "binance-key", "encrypted-secret", "Primary");
            });

        encryption
            .Setup(service => service.Decrypt("encrypted-secret"))
            .Returns("plain-secret");

        services.AddScoped(_ => repository.Object);
        services.AddScoped(_ => encryption.Object);
        var provider = services.BuildServiceProvider();
        var httpContextAccessor = CreateHttpContextAccessor();
        var accessor = new CurrentUserExchangeCredentialAccessor(
            new IdentityService(httpContextAccessor),
            httpContextAccessor,
            provider.GetRequiredService<IServiceScopeFactory>());

        var firstCall = accessor.GetActiveCredentialAsync(Exchange.Binance);
        var secondCall = accessor.GetActiveCredentialAsync(Exchange.Binance);

        gate.SetResult();
        var results = await Task.WhenAll(firstCall, secondCall);

        results.Should().NotContainNulls();
        var first = results[0]!;
        var second = results[1]!;
        first.ApiKey.Should().Be("binance-key");
        first.ApiSecret.Should().Be("plain-secret");
        second.ApiKey.Should().Be("binance-key");
        repository.Verify(repo => repo.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()), Times.Once);
        encryption.Verify(service => service.Decrypt("encrypted-secret"), Times.Once);
    }

    [TestMethod]
    public async Task GivenRepeatedBinanceLookups_WhenCredentialAlreadyResolved_ThenReturnsCachedSnapshot()
    {
        var services = new ServiceCollection();
        var repository = new Mock<IUserExchangeCredentialRepository>();
        var encryption = new Mock<ICredentialEncryptionService>();

        repository
            .Setup(repo => repo.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserExchangeCredential.Create(TestUserId, Exchange.Binance, "binance-key", "encrypted-secret", "Primary"));

        encryption
            .Setup(service => service.Decrypt("encrypted-secret"))
            .Returns("plain-secret");

        services.AddScoped(_ => repository.Object);
        services.AddScoped(_ => encryption.Object);
        var provider = services.BuildServiceProvider();
        var httpContextAccessor = CreateHttpContextAccessor();
        var accessor = new CurrentUserExchangeCredentialAccessor(
            new IdentityService(httpContextAccessor),
            httpContextAccessor,
            provider.GetRequiredService<IServiceScopeFactory>());

        var first = await accessor.GetActiveCredentialAsync(Exchange.Binance);
        var second = await accessor.GetActiveCredentialAsync(Exchange.Binance);

        first.Should().NotBeNull();
        second.Should().BeEquivalentTo(first);
        repository.Verify(repo => repo.GetActiveByUserIdAndExchangeAsync(TestUserId, Exchange.Binance, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static HttpContextAccessor CreateHttpContextAccessor()
    {
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                    new Claim(ClaimTypes.Email, "binance@test.dev"),
                ],
                authenticationType: "Test")),
            },
        };
    }
}