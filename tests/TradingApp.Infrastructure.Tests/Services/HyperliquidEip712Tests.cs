using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidEip712Tests
{
    [TestMethod]
    public void GivenKnownOrderAction_WhenComputeActionHash_ThenReturns32ByteHash()
    {
        var action = HyperliquidEip712.BuildOrderAction(assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        var hash = HyperliquidEip712.ComputeActionHash(action, nonce: 1716499200000L, vaultAddress: null);

        hash.Should().NotBeNull();
        hash.Should().HaveCount(32);
    }

    [TestMethod]
    public void GivenSameInputs_WhenComputeActionHashTwice_ThenReturnsSameHash()
    {
        var action = HyperliquidEip712.BuildOrderAction(assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        var hashOne = HyperliquidEip712.ComputeActionHash(action, nonce: 1716499200000L, vaultAddress: null);
        var hashTwo = HyperliquidEip712.ComputeActionHash(action, nonce: 1716499200000L, vaultAddress: null);

        hashOne.Should().Equal(hashTwo);
    }

    [TestMethod]
    public void GivenDifferentNonces_WhenComputeActionHash_ThenReturnsDifferentHashes()
    {
        var action = HyperliquidEip712.BuildOrderAction(assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        var hashOne = HyperliquidEip712.ComputeActionHash(action, nonce: 1716499200000L, vaultAddress: null);
        var hashTwo = HyperliquidEip712.ComputeActionHash(action, nonce: 1716499200001L, vaultAddress: null);

        hashOne.Should().NotEqual(hashTwo);
    }

    [TestMethod]
    public void GivenTestnetFlag_WhenBuildPhantomAgentTypedData_ThenUsesTestnetSource()
    {
        var connectionId = Enumerable.Repeat((byte)1, 32).ToArray();

        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

        typedData.Domain.Name.Should().Be("Exchange");
        typedData.Domain.Version.Should().Be("1");
        typedData.Domain.ChainId.ToString().Should().Be(HyperliquidEip712.PhantomAgentChainId.ToString());
        typedData.Domain.VerifyingContract.Should().Be("0x0000000000000000000000000000000000000000");
        typedData.PrimaryType.Should().Be("Agent");
        typedData.Types.Should().ContainKey("Agent");
    }

    [TestMethod]
    public void GivenMainnetFlag_WhenBuildPhantomAgentTypedData_ThenUsesMainnetSource()
    {
        var signer = HyperliquidSigner.Create("0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e");
        var connectionId = Enumerable.Repeat((byte)2, 32).ToArray();

        var testnetTypedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);
        var mainnetTypedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: true);

        var testnetSignature = signer.SignTypedData(testnetTypedData);
        var mainnetSignature = signer.SignTypedData(mainnetTypedData);

        mainnetSignature.Should().NotBe(testnetSignature);
    }

    [TestMethod]
    public void GivenConnectionIdWithInvalidLength_WhenBuildPhantomAgentTypedData_ThenThrowsArgumentException()
    {
        var invalidConnectionId = Enumerable.Repeat((byte)1, 31).ToArray();

        var action = () => HyperliquidEip712.BuildPhantomAgentTypedData(invalidConnectionId, isMainnet: false);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [TestMethod]
    public void GivenOrderInputs_WhenBuildOrderAction_ThenReturnsExpectedOrderShape()
    {
        var action = HyperliquidEip712.BuildOrderAction(assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        action["type"].Should().Be("order");
        action["grouping"].Should().Be("na");

        var orders = (Dictionary<string, object>[])action["orders"];
        orders.Should().HaveCount(1);
        orders[0]["a"].Should().Be(0);
        orders[0]["b"].Should().Be(true);
        orders[0]["r"].Should().Be(false);
    }
}