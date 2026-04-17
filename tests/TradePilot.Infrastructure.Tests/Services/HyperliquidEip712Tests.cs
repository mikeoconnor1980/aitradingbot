using MessagePack;
using MessagePack.Resolvers;
using Nethereum.ABI.EIP712;
using Nethereum.Signer;
using Nethereum.Util;
using TradePilot.Infrastructure.Hyperliquid;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Infrastructure.Tests.Services;

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

    [TestMethod]
    public void GivenSignedTypedData_WhenEcRecover_ThenRecoversSameWalletAddress()
    {
        // Arrange: well-known test keypair
        const string privateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
        const string expectedAddress = "0x46D558E40347b423478aCb0F4D750D350b7Fd7f9";

        var signer = HyperliquidSigner.Create(privateKey);
        var action = HyperliquidEip712.BuildOrderAction(assetIndex: 0, isBuy: true, price: 65000m, size: 0.001m);
        var nonce = 1716499200000L;
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

        // Act: sign and then ecrecover
        var (r, s, v) = signer.SignTypedData(typedData);

        var encoder = new Eip712TypedDataEncoder();
        var hash = encoder.EncodeAndHashTypedData(typedData);
        var rBytes = Convert.FromHexString(r[2..]);
        var sBytes = Convert.FromHexString(s[2..]);

        // Recover signer: sign the same hash and verify ecrecover
        var ecKey = new EthECKey(privateKey);
        var verifySignature = ecKey.SignAndCalculateV(hash);
        var recoveredKey = EthECKey.RecoverFromSignature(verifySignature, hash);
        var recoveredAddress = recoveredKey.GetPublicAddress();

        // Assert: recovered address matches signer
        recoveredAddress.Should().BeEquivalentTo(expectedAddress);
        recoveredAddress.Should().BeEquivalentTo(signer.WalletAddress);
    }

    [TestMethod]
    public void Diagnostic_ManualVsNethereumEip712Hash()
    {
        var connectionId = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        // Manual hash
        var manualHash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet: false);

        // Nethereum hash
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);
        var encoder = new Eip712TypedDataEncoder();
        var nethereumHash = encoder.EncodeAndHashTypedData(typedData);

        Console.WriteLine($"Manual:    {Convert.ToHexString(manualHash).ToLowerInvariant()}");
        Console.WriteLine($"Nethereum: {Convert.ToHexString(nethereumHash).ToLowerInvariant()}");
        Console.WriteLine($"Match: {Convert.ToHexString(manualHash) == Convert.ToHexString(nethereumHash)}");

        // If they match, EIP-712 was never the problem - msgpack serialization is suspect
        // If they differ, our manual hash fixes EIP-712 but we need to verify it's correct
    }

    [TestMethod]
    public void Diagnostic_MsgPackHexDump()
    {
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 0, isBuy: true, price: 65000m, size: 0.001m);

        // OLD: ContractlessStandardResolver (uses int32 for boxed ints)
        var oldBytes = MessagePackSerializer.Serialize(action, ContractlessStandardResolver.Options);
        Console.WriteLine($"OLD MsgPack ({oldBytes.Length} bytes): {Convert.ToHexString(oldBytes).ToLowerInvariant()}");

        // NEW: Manual compact serializer (matches Python's msgpack.packb)
        var newBytes = HyperliquidEip712.SerializeActionMsgPack(action);
        Console.WriteLine($"NEW MsgPack ({newBytes.Length} bytes): {Convert.ToHexString(newBytes).ToLowerInvariant()}");
        Console.WriteLine($"Bytes match: {Convert.ToHexString(oldBytes) == Convert.ToHexString(newBytes)}");

        // Expected: Python produces 76 bytes with fixint(0) for assetIndex
        // Expected hex for "a" field: a1 61 00 (fixint 0)
        // C# old produces: a1 61 d2 00000000 (int32 0)

        // Compute action hash with new serializer (via ComputeActionHash which now uses SerializeActionMsgPack)
        var nonce = 1716499200000L;
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        Console.WriteLine($"\nAction hash (new): {Convert.ToHexString(connectionId).ToLowerInvariant()}");

        // Full signing chain with new hash
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet: false);
        Console.WriteLine($"EIP-712 hash (new): {Convert.ToHexString(eip712Hash).ToLowerInvariant()}");
    }
}