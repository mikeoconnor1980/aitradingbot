using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class UserExchangeCredentialTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var credential = UserExchangeCredential.Create(Guid.NewGuid(), Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance");

        credential.Id.Should().NotBeEmpty();
        credential.Exchange.Should().Be(Exchange.Binance);
        credential.ApiKey.Should().Be("api-key");
        credential.EncryptedApiSecret.Should().Be("encrypted-secret");
        credential.Label.Should().Be("Primary Binance");
        credential.IsActive.Should().BeTrue();
        credential.CreatedAtUtc.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenHyperliquidExchange_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => UserExchangeCredential.Create(Guid.NewGuid(), Exchange.Hyperliquid, "api-key", "encrypted-secret", "Primary");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GivenCredential_WhenUpdateSecrets_ThenValuesUpdated()
    {
        var credential = UserExchangeCredential.Create(Guid.NewGuid(), Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance");

        credential.UpdateSecrets("new-key", "new-secret", "Backup Binance");

        credential.ApiKey.Should().Be("new-key");
        credential.EncryptedApiSecret.Should().Be("new-secret");
        credential.Label.Should().Be("Backup Binance");
    }

    [TestMethod]
    public void GivenCredential_WhenDeactivate_ThenIsActiveFalse()
    {
        var credential = UserExchangeCredential.Create(Guid.NewGuid(), Exchange.Binance, "api-key", "encrypted-secret", "Primary Binance");

        credential.Deactivate();

        credential.IsActive.Should().BeFalse();
    }
}