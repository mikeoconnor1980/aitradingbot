using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidSignerTests
{
    // Well-known test key pair (Ethereum testnet - never use on mainnet)
    private const string ValidPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private const string ExpectedAddress = "0x46D558E40347b423478aCb0F4D750D350b7Fd7f9";

    [TestMethod]
    public void GivenValidPrivateKey_WhenCreate_ThenDerivesCorrectWalletAddress()
    {
        var signer = HyperliquidSigner.Create(ValidPrivateKey);

        signer.WalletAddress.Should().BeEquivalentTo(ExpectedAddress);
    }

    [TestMethod]
    public void GivenPrivateKeyWithout0xPrefix_WhenCreate_ThenDerivesCorrectWalletAddress()
    {
        var keyWithoutPrefix = ValidPrivateKey[2..];

        var signer = HyperliquidSigner.Create(keyWithoutPrefix);

        signer.WalletAddress.Should().BeEquivalentTo(ExpectedAddress);
    }

    [TestMethod]
    public void GivenEmptyPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [TestMethod]
    public void GivenNullPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create(null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [TestMethod]
    public void GivenMalformedPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create("not-a-valid-key");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*malformed*");
    }

    [TestMethod]
    public void GivenTooShortPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create("0x1234");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*malformed*");
    }

    [TestMethod]
    public void GivenValidTypedData_WhenSignTypedData_ThenReturnsSignatureComponents()
    {
        var signer = HyperliquidSigner.Create(ValidPrivateKey);
        var connectionId = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

        var (r, s, v) = signer.SignTypedData(typedData);

        r.Should().StartWith("0x").And.HaveLength(66);
        s.Should().StartWith("0x").And.HaveLength(66);
        v.Should().BeOneOf(27, 28);
    }

    [TestMethod]
    public void GivenSameTypedData_WhenSignTypedDataTwice_ThenProducesDeterministicSignature()
    {
        var signer = HyperliquidSigner.Create(ValidPrivateKey);
        var connectionId = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

        var signatureOne = signer.SignTypedData(typedData);
        var signatureTwo = signer.SignTypedData(typedData);

        signatureOne.Should().Be(signatureTwo);
    }
}
