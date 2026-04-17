using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;
using Eip712Domain = Nethereum.ABI.EIP712.Domain;

namespace TradePilot.Infrastructure.Hyperliquid;

public static class HyperliquidEip712
{
    public const int PhantomAgentChainId = 1337;
    public const string TestnetSource = "b";
    public const string MainnetSource = "a";

    public static byte[] ComputeActionHash(object action, long nonce, string? vaultAddress)
    {
        var actionBytes = SerializeActionMsgPack(action);
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

    public static TypedData<Eip712Domain> BuildPhantomAgentTypedData(byte[] connectionId, bool isMainnet)
    {
        if (connectionId.Length != 32)
        {
            throw new ArgumentException("Connection ID must be 32 bytes.", nameof(connectionId));
        }

        var source = isMainnet ? MainnetSource : TestnetSource;
        var typedData = new TypedData<Eip712Domain>
        {
            Domain = new Eip712Domain
            {
                Name = "Exchange",
                Version = "1",
                ChainId = PhantomAgentChainId,
                VerifyingContract = "0x0000000000000000000000000000000000000000"
            },
            PrimaryType = nameof(Agent),
            Types = new Dictionary<string, MemberDescription[]>
            {
                ["EIP712Domain"] =
                [
                    new MemberDescription { Name = "name", Type = "string" },
                    new MemberDescription { Name = "version", Type = "string" },
                    new MemberDescription { Name = "chainId", Type = "uint256" },
                    new MemberDescription { Name = "verifyingContract", Type = "address" },
                ],
                [nameof(Agent)] =
                [
                    new MemberDescription { Name = "source", Type = "string" },
                    new MemberDescription { Name = "connectionId", Type = "bytes32" },
                ],
            },
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

    public static Dictionary<string, object> BuildTriggerOrderAction(
        int assetIndex,
        bool isBuy,
        decimal triggerPrice,
        decimal size,
        string tpsl)
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
                    ["p"] = ToWireDecimal(triggerPrice),
                    ["s"] = ToWireDecimal(size),
                    ["r"] = true,
                    ["t"] = new Dictionary<string, object>
                    {
                        ["trigger"] = new Dictionary<string, object>
                        {
                            ["isMarket"] = true,
                            ["triggerPx"] = ToWireDecimal(triggerPrice),
                            ["tpsl"] = tpsl,
                        },
                    },
                },
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

    /// <summary>
    /// Serializes an action dictionary to MessagePack bytes using compact encoding,
    /// matching Python's msgpack.packb output byte-for-byte.
    /// The default ContractlessStandardResolver uses int32 wire format for boxed ints,
    /// but Python uses the most compact form (e.g. fixint for 0-127).
    /// </summary>
    public static byte[] SerializeActionMsgPack(object action)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(bufferWriter);

        if (action is Dictionary<string, object> dictionary)
        {
            WriteMsgPackValue(ref writer, dictionary);
        }
        else
        {
            var jsonElement = JsonSerializer.SerializeToElement(action);
            WriteMsgPackValue(ref writer, jsonElement);
        }

        writer.Flush();
        return bufferWriter.WrittenSpan.ToArray();
    }

    private static void WriteMsgPackValue(ref MessagePackWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNil();
                break;
            case string s:
                writer.Write(s);
                break;
            case bool b:
                writer.Write(b);
                break;
            case int i:
                writer.Write(i);
                break;
            case long l:
                writer.Write(l);
                break;
            case double d:
                writer.Write(d);
                break;
            case JsonElement jsonElement:
                WriteJsonElement(ref writer, jsonElement);
                break;
            case Dictionary<string, object> dict:
                writer.WriteMapHeader(dict.Count);
                foreach (var kvp in dict)
                {
                    writer.Write(kvp.Key);
                    WriteMsgPackValue(ref writer, kvp.Value);
                }
                break;
            case IList list:
                writer.WriteArrayHeader(list.Count);
                foreach (var item in list)
                {
                    WriteMsgPackValue(ref writer, item);
                }
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported msgpack value type: {value.GetType().Name}");
        }
    }

    private static void WriteJsonElement(ref MessagePackWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var properties = element.EnumerateObject().ToArray();
                writer.WriteMapHeader(properties.Length);
                foreach (var property in properties)
                {
                    writer.Write(property.Name);
                    WriteJsonElement(ref writer, property.Value);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var items = element.EnumerateArray().ToArray();
                writer.WriteArrayHeader(items.Length);
                foreach (var item in items)
                {
                    WriteJsonElement(ref writer, item);
                }

                break;
            }
            case JsonValueKind.String:
                writer.Write(element.GetString());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.Write(element.GetBoolean());
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                {
                    writer.Write(intValue);
                }
                else if (element.TryGetInt64(out var longValue))
                {
                    writer.Write(longValue);
                }
                else if (element.TryGetDouble(out var doubleValue))
                {
                    writer.Write(doubleValue);
                }
                else
                {
                    writer.Write(element.GetRawText());
                }

                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNil();
                break;
            default:
                throw new NotSupportedException($"Unsupported JsonElement kind: {element.ValueKind}");
        }
    }

    /// <summary>
    /// Computes the EIP-712 hash manually, matching the Hyperliquid Python SDK exactly.
    /// This bypasses Nethereum's Eip712TypedDataEncoder to avoid domain type hash discrepancies.
    /// </summary>
    public static byte[] ComputeEip712Hash(byte[] connectionId, bool isMainnet)
    {
        if (connectionId.Length != 32)
        {
            throw new ArgumentException("Connection ID must be 32 bytes.", nameof(connectionId));
        }

        var keccak = Sha3Keccack.Current;

        // 1. Domain type hash: keccak256("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)")
        var domainTypeHash = keccak.CalculateHash(
            Encoding.UTF8.GetBytes("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)"));

        // 2. Domain separator: keccak256(domainTypeHash ‖ keccak256("Exchange") ‖ keccak256("1") ‖ pad32(1337) ‖ pad32(0x0))
        var nameHash = keccak.CalculateHash(Encoding.UTF8.GetBytes("Exchange"));
        var versionHash = keccak.CalculateHash(Encoding.UTF8.GetBytes("1"));

        var chainIdBytes = new byte[32];
        chainIdBytes[31] = (byte)(PhantomAgentChainId & 0xFF);
        chainIdBytes[30] = (byte)((PhantomAgentChainId >> 8) & 0xFF);

        var verifyingContractBytes = new byte[32]; // zero address, left-padded to 32 bytes

        var domainInput = new byte[5 * 32];
        Array.Copy(domainTypeHash, 0, domainInput, 0, 32);
        Array.Copy(nameHash, 0, domainInput, 32, 32);
        Array.Copy(versionHash, 0, domainInput, 64, 32);
        Array.Copy(chainIdBytes, 0, domainInput, 96, 32);
        Array.Copy(verifyingContractBytes, 0, domainInput, 128, 32);
        var domainSeparator = keccak.CalculateHash(domainInput);

        // 3. Agent type hash: keccak256("Agent(string source,bytes32 connectionId)")
        var agentTypeHash = keccak.CalculateHash(
            Encoding.UTF8.GetBytes("Agent(string source,bytes32 connectionId)"));

        // 4. Struct hash: keccak256(agentTypeHash ‖ keccak256(source) ‖ connectionId)
        var source = isMainnet ? MainnetSource : TestnetSource;
        var sourceHash = keccak.CalculateHash(Encoding.UTF8.GetBytes(source));

        var structInput = new byte[3 * 32];
        Array.Copy(agentTypeHash, 0, structInput, 0, 32);
        Array.Copy(sourceHash, 0, structInput, 32, 32);
        Array.Copy(connectionId, 0, structInput, 64, 32);
        var structHash = keccak.CalculateHash(structInput);

        // 5. Final hash: keccak256("\x19\x01" ‖ domainSeparator ‖ structHash)
        var finalInput = new byte[2 + 32 + 32];
        finalInput[0] = 0x19;
        finalInput[1] = 0x01;
        Array.Copy(domainSeparator, 0, finalInput, 2, 32);
        Array.Copy(structHash, 0, finalInput, 34, 32);

        return keccak.CalculateHash(finalInput);
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