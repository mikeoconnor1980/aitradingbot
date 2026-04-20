using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class UserExchangeCredentialRepositoryTests
{
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase($"UserExchangeCredentialRepositoryTests-{Guid.NewGuid():N}")
            .Options;

        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenActiveCredential_WhenGetActiveByUserIdAndExchangeAsync_ThenReturnsMatch()
    {
        var user = User.Create("user@example.com", "User", "hash");
        var credential = UserExchangeCredential.Create(user.Id, Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance");

        await using (var context = CreateContext())
        {
            context.Users.Add(user);
            context.UserExchangeCredentials.Add(credential);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = CreateContext();
        var repository = new UserExchangeCredentialRepository(verifyContext);

        var result = await repository.GetActiveByUserIdAndExchangeAsync(user.Id, Exchange.Binance);

        result.Should().NotBeNull();
        result!.ApiKey.Should().Be("api-key");
    }

    [TestMethod]
    public async Task GivenActiveCredential_WhenGetAllActiveByUserIdAsync_ThenReturnsAll()
    {
        var user = User.Create("user@example.com", "User", "hash");
        var credential = UserExchangeCredential.Create(user.Id, Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance");

        await using (var context = CreateContext())
        {
            context.Users.Add(user);
            context.UserExchangeCredentials.Add(credential);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = CreateContext();
        var repository = new UserExchangeCredentialRepository(verifyContext);

        var results = await repository.GetAllActiveByUserIdAsync(user.Id);

        results.Should().HaveCount(1);
        results[0].Exchange.Should().Be(Exchange.Binance);
    }
}