using System.Globalization;
using MessagePack;
using MessagePack.Resolvers;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;

namespace TradingApp.Infrastructure.Hyperliquid;

public static class HyperliquidEip712
{
    public const int PhantomAgentChainId = 1337;
    public const string TestnetSource = "b";
    public const string MainnetSource = "a";

    public static byte[] ComputeActionHash(object action, long nonce, string? vaultAddress)
    {
        var actionBytes = MessagePackSerializer.Serialize(action, ContractlessStandardResolver.Options);
        var nonceBytes = BitConverter.GetBytes(nonce);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(nonceBytes);
        }

        var input = new List<byte>(actionBytes.Length + nonceBytes.Length + 22);
        input.AddRange(actionBytes);
        input.AddRange(nonceBytes);

        if (string.IsNullOrWhiteSpace(vaultAddress))
        {
            input.Add(0x00);
        }
        else
        {
            input.Add(0x01);
            input.AddRange(ParseAddress(vaultAddress));
        }

        return Sha3Keccack.Current.CalculateHash(input.ToArray());
    }

    public static TypedData<Domain> BuildPhantomAgentTypedData(byte[] connectionId, bool isMainnet)
    {
        if (connectionId.Length != 32)
        {
            throw new ArgumentException("Connection ID must be 32 bytes.", nameof(connectionId));
        }

        var source = isMainnet ? MainnetSource : TestnetSource;
        var typedData = new TypedData<Domain>
        {
            Domain = new Domain
            {
                Name = "Exchange",
                Version = "1",
                ChainId = PhantomAgentChainId,
                VerifyingContract = "0x0000000000000000000000000000000000000000"
            },
            PrimaryType = nameof(Agent),
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Agent)),
        };

        typedData.SetMessage(new Agent
        {
            Source = source,
            ConnectionId = connectionId
        });

        return typedData;
    }

    public static Dictionary<string, object> BuildOrderAction(
        int assetIndex,
        bool isBuy,
        decimal price,
        decimal size,
        bool reduceOnly = false,
        string tif = "Gtc")
    {
        return new Dictionary<string, object>
        {
            ["type"] = "order",
            ["orders"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["a"] = assetIndex,
                    ["b"] = isBuy,
                    ["p"] = ToWireDecimal(price),
                    ["s"] = ToWireDecimal(size),
                    ["r"] = reduceOnly,
                    ["t"] = new Dictionary<string, object>
                    {
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["tif"] = tif,
                        }
                    }
                }
            },
            ["grouping"] = "na"
        };
    }

    private static byte[] ParseAddress(string address)
    {
        var normalised = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? address[2..]
            : address;

        if (normalised.Length != 40)
        {
            throw new ArgumentException("Vault address must be 20 bytes (40 hex characters).", nameof(address));
        }

        return Convert.FromHexString(normalised);
    }

    private static string ToWireDecimal(decimal value)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return formatted.Contains('.')
            ? formatted.TrimEnd('0').TrimEnd('.')
            : formatted;
    }
}

[Struct("Agent")]
public sealed class Agent
{
    [Parameter("string", "source", 1)]
    public string Source { get; set; } = default!;

    [Parameter("bytes32", "connectionId", 2)]
    public byte[] ConnectionId { get; set; } = default!;
}