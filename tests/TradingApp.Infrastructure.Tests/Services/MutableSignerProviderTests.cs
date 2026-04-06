using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class MutableSignerProviderTests
{
    private const string ValidKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private const string ValidKey2 = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

    private MutableSignerProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = new Mock<ILogger<MutableSignerProvider>>();
        _sut = new MutableSignerProvider(logger.Object);
    }

    [TestMethod]
    public void IsConfigured_InitiallyFalse()
    {
        _sut.IsConfigured.Should().BeFalse();
    }

    [TestMethod]
    public void Configure_WithValidKey_SetsIsConfiguredTrue()
    {
        _sut.Configure(ValidKey);

        _sut.IsConfigured.Should().BeTrue();
    }

    [TestMethod]
    public void Configure_WithValidKey_ExposesWalletAddress()
    {
        _sut.Configure(ValidKey);

        _sut.WalletAddress.Should().NotBeNullOrWhiteSpace();
        _sut.WalletAddress.Should().StartWith("0x");
    }

    [TestMethod]
    public void Configure_WithDifferentKey_UpdatesWalletAddress()
    {
        _sut.Configure(ValidKey);
        var firstAddress = _sut.WalletAddress;

        _sut.Configure(ValidKey2);
        var secondAddress = _sut.WalletAddress;

        secondAddress.Should().NotBe(firstAddress);
    }

    [TestMethod]
    public void WalletAddress_WhenNotConfigured_Throws()
    {
        var act = () => _sut.WalletAddress;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configure*");
    }

    [TestMethod]
    public void SignHash_WhenNotConfigured_Throws()
    {
        var act = () => _sut.SignHash(new byte[32]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configure*");
    }

    [TestMethod]
    public void SignHash_WhenConfigured_ReturnsTuple()
    {
        _sut.Configure(ValidKey);

        var (r, s, v) = _sut.SignHash(new byte[32]);

        r.Should().StartWith("0x");
        s.Should().StartWith("0x");
        v.Should().BeOneOf(27, 28);
    }

    [TestMethod]
    public void Clear_AfterConfigure_SetsIsConfiguredFalse()
    {
        _sut.Configure(ValidKey);
        _sut.Clear();

        _sut.IsConfigured.Should().BeFalse();
    }

    [TestMethod]
    public void Clear_AfterConfigure_WalletAddressThrows()
    {
        _sut.Configure(ValidKey);
        _sut.Clear();

        var act = () => _sut.WalletAddress;

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Configure_WithInvalidKey_ThrowsArgumentException()
    {
        var act = () => _sut.Configure("not-a-key");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Configure_WithEmptyKey_ThrowsArgumentException()
    {
        var act = () => _sut.Configure("");

        act.Should().Throw<ArgumentException>();
    }
}
