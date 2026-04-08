using TradingApp.Domain.Entities;

namespace TradingApp.Domain.Tests.Entities;

[TestClass]
public sealed class UserWalletAddressTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const string ValidAddress = "0xAbCdEf1234567890AbCdEf1234567890AbCdEf12";

    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var wallet = UserWalletAddress.Create(ValidUserId, ValidAddress);

        wallet.Id.Should().NotBeEmpty();
        wallet.UserId.Should().Be(ValidUserId);
        wallet.WalletAddress.Should().Be(ValidAddress);
        wallet.Exchange.Should().Be("Hyperliquid");
        wallet.IsActive.Should().BeTrue();
        wallet.CreatedAtUtc.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenEmptyUserId_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => UserWalletAddress.Create(Guid.Empty, ValidAddress);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidWalletAddress_WhenCreate_ThenThrowsArgumentException(string? address)
    {
        var act = () => UserWalletAddress.Create(ValidUserId, address!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow("0x123")]
    [DataRow("not-an-address")]
    [DataRow("0xGHIJKL1234567890AbCdEf1234567890AbCdEf12")]
    [DataRow("AbCdEf1234567890AbCdEf1234567890AbCdEf12")]
    public void GivenBadFormatAddress_WhenCreate_ThenThrowsArgumentException(string address)
    {
        var act = () => UserWalletAddress.Create(ValidUserId, address);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Ethereum address*");
    }

    [TestMethod]
    public void GivenWallet_WhenUpdateAddress_ThenAddressUpdated()
    {
        var wallet = UserWalletAddress.Create(ValidUserId, ValidAddress);
        var newAddress = "0x1111111111111111111111111111111111111111";

        wallet.UpdateAddress(newAddress);

        wallet.WalletAddress.Should().Be(newAddress);
    }

    [TestMethod]
    public void GivenWallet_WhenUpdateWithInvalidAddress_ThenThrows()
    {
        var wallet = UserWalletAddress.Create(ValidUserId, ValidAddress);

        var act = () => wallet.UpdateAddress("bad-address");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GivenWallet_WhenDeactivate_ThenIsActiveFalse()
    {
        var wallet = UserWalletAddress.Create(ValidUserId, ValidAddress);

        wallet.Deactivate();

        wallet.IsActive.Should().BeFalse();
    }
}
