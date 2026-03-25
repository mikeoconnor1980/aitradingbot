<!-- markdownlint-disable-file -->

# Task Details: F5 — Order Placement

## Phase 1: EIP-712 Signing & Nonce Infrastructure

This is the critical risk retirement phase. If Nethereum's EIP-712 implementation is not compatible with Hyperliquid's expected signature format, this is where the blocker will be discovered.

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, `_camelCase` fields, Guard.Against validation
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, `Given_When_Then` naming
- `.github/instructions/dotnet-architecture.instructions.md` — Layer boundaries, service interface placement
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Exchange API, authentication, signing extension rules
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 12: Nethereum for signing

## Design References

### Hyperliquid EIP-712 Signing Algorithm

Hyperliquid uses a "phantom agent" EIP-712 pattern for testnet. The signing flow:

1. **Action payload**: Build the order action JSON object (type, orders array, grouping)
2. **Action hash**: `SHA256(msgpack(action) + nonce_as_8_byte_big_endian + vault_indicator)`
   - `vault_indicator` = `0x00` when vaultAddress is null (no vault)
   - MessagePack serialization must match Python's `msgpack.packb(action)` byte output
3. **Phantom agent**: `{ source: "b", connectionId: action_hash_bytes }` (testnet uses `"b"`)
4. **EIP-712 domain**: `{ name: "Exchange", version: "1", chainId: 1337, verifyingContract: "0x0000000000000000000000000000000000000000" }`
5. **EIP-712 types**: `{ Agent: [{ name: "source", type: "string" }, { name: "connectionId", type: "bytes32" }] }`
6. **Sign**: Hash the EIP-712 structured data and sign with `EthECKey.SignAndCalculateV(hash)` → produces `(v, r, s)`

**CRITICAL**: The Hyperliquid Python SDK is the authoritative reference for the exact signing algorithm. The implementing agent MUST verify each step against the Python SDK source (`hyperliquid-python-sdk` on GitHub, specifically `hyperliquid/exchange.py` and `hyperliquid/utils/signing.py`).

### Key Nethereum APIs

```
Nethereum.Signer.EIP712.Eip712TypedDataSigner — Signs EIP-712 typed data
Nethereum.ABI.EIP712.TypedData<Domain> — Typed data structure
Nethereum.ABI.EIP712.MemberDescription — Type field definitions
Nethereum.Signer.EthECKey — ECDSA key pair
Nethereum.Util.Sha3Keccack — Keccak-256 hashing
```

---

### Task 1.1: Add NuGet dependencies {#task-11-add-nuget-dependencies}

Add `Nethereum.ABI` and `MessagePack` NuGet packages to the Infrastructure project for EIP-712 typed data and action hash computation.

- **Complexity**: Low
- **Risk Factors**: Version compatibility with existing `Nethereum.Signer 6.0.4`
- **Files**:
  - `src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj` — Add `Nethereum.ABI` and `MessagePack` package references
  - `src/TradingApp.Application/TradingApp.Application.csproj` — Add `Nethereum.ABI` package reference (needed for `TypedData<TDomain>` and `IDomain` types in the `IHyperliquidSigner` interface, Task 1.3)
- **Success**:
  - `Nethereum.ABI` package added to Infrastructure and Application projects, version-compatible with existing `Nethereum.Signer 6.0.4`
  - `MessagePack` package added to Infrastructure
  - Solution builds without errors
- **Dependencies**: None

#### Implementation Details

First verify whether `Nethereum.ABI` types are already available transitively from `Nethereum.Signer`:

```bash
dotnet list src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj package --include-transitive | findstr "Nethereum.ABI"
```

If not available, add explicitly:

```bash
cd src/TradingApp.Infrastructure
dotnet add package Nethereum.ABI --version 6.0.4
dotnet add package MessagePack
cd ../TradingApp.Application
dotnet add package Nethereum.ABI --version 6.0.4
```

The version of `Nethereum.ABI` should match the major version of the existing `Nethereum.Signer` (6.x) to avoid conflicts.

##### Pattern References

- `src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj` — Existing Nethereum.Signer reference
- `src/TradingApp.Application/TradingApp.Application.csproj` — Application project for interface types

---

### Task 1.2: Create Hyperliquid EIP-712 type definitions {#task-12-create-hyperliquid-eip-712-type-definitions}

Create a static helper class that defines Hyperliquid's EIP-712 domain, type structures, and the action hash computation (MessagePack + SHA-256).

- **Complexity**: High
- **Risk Factors**: MessagePack byte-level compatibility with Python SDK; incorrect EIP-712 domain or type definitions will cause silent signature rejection
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — New file: EIP-712 constants, type definitions, action hash computation
- **Success**:
  - EIP-712 domain separator hash matches Python SDK output for testnet
  - Action hash computation produces identical bytes to Python SDK's `action_hash()` function
  - Type definitions match Hyperliquid's expected `Agent` primary type
- **Dependencies**: Task 1.1 (NuGet packages)

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs — new file
using System.Security.Cryptography;
using MessagePack;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace TradingApp.Infrastructure.Hyperliquid;

/// <summary>
/// Defines Hyperliquid's EIP-712 domain, type structures, and action hash computation.
/// Reference: https://github.com/hyperliquid-dex/hyperliquid-python-sdk
/// </summary>
public static class HyperliquidEip712
{
    /// <summary>
    /// Hyperliquid uses its own chain ID (1337) for EIP-712 phantom agent signing,
    /// NOT the Arbitrum chain IDs.
    /// </summary>
    public const int PhantomAgentChainId = 1337;

    /// <summary>
    /// Testnet phantom agent source identifier.
    /// Mainnet uses "a", testnet uses "b".
    /// </summary>
    public const string TestnetSource = "b";

    /// <summary>
    /// Mainnet phantom agent source identifier.
    /// </summary>
    public const string MainnetSource = "a";

    private static readonly byte[] ZeroAddress = new byte[20]; // verifyingContract = 0x000...000

    /// <summary>
    /// Computes the action hash used as the connectionId for phantom agent signing.
    /// Algorithm: SHA256(msgpack(action) + nonce_as_8_byte_big_endian + vault_indicator)
    /// </summary>
    public static byte[] ComputeActionHash(object action, long nonce, string? vaultAddress)
    {
        var actionBytes = MessagePackSerializer.Serialize(action, MessagePack.Resolvers.ContractlessStandardResolver.Options);
        var nonceBytes = BitConverter.GetBytes(nonce);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(nonceBytes); // Convert to big-endian

        byte[] vaultBytes;
        if (vaultAddress is not null)
        {
            vaultBytes = Convert.FromHexString(
                vaultAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? vaultAddress[2..]
                    : vaultAddress);
        }
        else
        {
            vaultBytes = [0x00];
        }

        var totalLength = actionBytes.Length + nonceBytes.Length + vaultBytes.Length;
        var combined = new byte[totalLength];
        Buffer.BlockCopy(actionBytes, 0, combined, 0, actionBytes.Length);
        Buffer.BlockCopy(nonceBytes, 0, combined, actionBytes.Length, nonceBytes.Length);
        Buffer.BlockCopy(vaultBytes, 0, combined, actionBytes.Length + nonceBytes.Length, vaultBytes.Length);

        return SHA256.HashData(combined);
    }

    /// <summary>
    /// Builds the EIP-712 TypedData structure for phantom agent signing.
    /// </summary>
    public static TypedData<PhantomAgentDomain> BuildPhantomAgentTypedData(
        byte[] connectionId,
        bool isMainnet)
    {
        return new TypedData<PhantomAgentDomain>
        {
            Domain = new PhantomAgentDomain
            {
                Name = "Exchange",
                Version = "1",
                ChainId = PhantomAgentChainId,
                VerifyingContract = "0x0000000000000000000000000000000000000000"
            },
            PrimaryType = nameof(Agent),
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(PhantomAgentDomain), typeof(Agent)),
            Message = new[]
            {
                new MemberValue { TypeName = "string", Value = isMainnet ? MainnetSource : TestnetSource },
                new MemberValue { TypeName = "bytes32", Value = connectionId }
            }
        };
    }

    /// <summary>
    /// Builds the Hyperliquid order action object for a single order.
    /// The structure must match Python SDK's action dict for correct MessagePack hash.
    /// </summary>
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
                    ["p"] = price.ToString("G"),
                    ["s"] = size.ToString("G"),
                    ["r"] = reduceOnly,
                    ["t"] = new Dictionary<string, object>
                    {
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["tif"] = tif
                        }
                    }
                }
            },
            ["grouping"] = "na"
        };
    }
}

/// <summary>
/// EIP-712 domain for Hyperliquid phantom agent signing.
/// </summary>
public sealed class PhantomAgentDomain : IDomain
{
    [Eip712Value("string")]
    public string Name { get; set; } = default!;

    [Eip712Value("string")]
    public string Version { get; set; } = default!;

    /// <summary>
    /// IMPORTANT: Verify that Nethereum's EIP-712 uint256 encoding works correctly with int.
    /// If the domain separator hash doesn't match the Python SDK, change this to BigInteger.
    /// </summary>
    [Eip712Value("uint256")]
    public int ChainId { get; set; }

    [Eip712Value("address")]
    public string VerifyingContract { get; set; } = default!;
}

/// <summary>
/// EIP-712 Agent type for phantom agent signing.
/// </summary>
public sealed class Agent
{
    [Eip712Value("string")]
    public string Source { get; set; } = default!;

    [Eip712Value("bytes32")]
    public byte[] ConnectionId { get; set; } = default!;
}
```

**CRITICAL VERIFICATION**: The implementing agent MUST verify the MessagePack serialization output by:
1. Serializing a known test action with Python `msgpack.packb(action)`
2. Serializing the same action with C# `MessagePackSerializer.Serialize(action, ContractlessStandardResolver.Options)`
3. Comparing the byte arrays — they MUST be identical for the SHA-256 hash to match

If bytes differ, investigate `ContractlessStandardResolver` vs `StandardResolver` options, or consider using `MessagePackWriter` for manual serialization with exact field ordering.

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Existing static helper in Hyperliquid namespace
- Hyperliquid Python SDK `hyperliquid/utils/signing.py` — Authoritative signing algorithm reference

---

### Task 1.3: Extend IHyperliquidSigner with signing method {#task-13-extend-ihyperliquidsigner-with-signing-method}

Add an EIP-712 signing method to the `IHyperliquidSigner` interface. The method signs typed data and returns the `(v, r, s)` signature components.

- **Complexity**: Medium
- **Risk Factors**: Interface change affects all consumers; must remain backward-compatible
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs` — Add signing method
- **Success**:
  - Interface has a new method that accepts typed data and returns signature components
  - Existing `WalletAddress` property unchanged
  - Solution builds
- **Dependencies**: Task 1.2 (EIP-712 types)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs — modification
using Nethereum.ABI.EIP712;

namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidSigner
{
    string WalletAddress { get; }

    /// <summary>
    /// Signs EIP-712 typed data and returns the signature components.
    /// </summary>
    /// <returns>Tuple of (R hex, S hex, V value) signature components.</returns>
    (string R, string S, int V) SignTypedData<TDomain>(TypedData<TDomain> typedData) where TDomain : IDomain;
}
```

**Note**: Adding a method to an interface does not break existing Moq mock setups in tests (Moq only verifies explicitly configured methods). However, the `HyperliquidSigner` implementation must implement the new method (Task 1.4).

The `TradingApp.Application` project will need a reference to `Nethereum.ABI` for the `TypedData<TDomain>` type. Add the NuGet reference to `TradingApp.Application.csproj` as well.

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs` — Current interface (only `WalletAddress`)
- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface extension pattern

---

### Task 1.4: Refactor HyperliquidSigner to implement signing {#task-14-refactor-hyperliquidsigner-to-implement-signing}

Modify `HyperliquidSigner` to retain the `EthECKey` instance (currently discarded after address derivation) and implement the `SignTypedData` method using Nethereum's `Eip712TypedDataSigner`.

- **Complexity**: Medium
- **Risk Factors**: The private key must be securely retained in memory for the app lifetime; `EthECKey` instance must not be exposed; existing tests must still pass
- **Files**:
  - `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` — Retain `EthECKey`, implement signing
- **Success**:
  - `HyperliquidSigner` stores `EthECKey` as a private field (not just the derived address)
  - `SignTypedData` produces valid EIP-712 signatures
  - `WalletAddress` still works correctly
  - Existing `HyperliquidSignerTests` pass without modification
- **Dependencies**: Task 1.2, Task 1.3

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs — modification
using Nethereum.ABI.EIP712;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidSigner : IHyperliquidSigner
{
    private readonly EthECKey _ecKey;

    public string WalletAddress { get; }

    private HyperliquidSigner(EthECKey ecKey, string walletAddress)
    {
        _ecKey = ecKey;
        WalletAddress = walletAddress;
    }

    public static HyperliquidSigner Create(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException(
                "Hyperliquid private key is missing. Set 'Hyperliquid__PrivateKey' environment variable or add 'Hyperliquid:PrivateKey' to appsettings.Development.json.",
                nameof(privateKey));
        }

        var normalised = privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? privateKey[2..]
            : privateKey;

        if (normalised.Length != 64 || !IsHex(normalised))
        {
            throw new ArgumentException(
                "Hyperliquid private key is malformed. Expected a 64-character hex string (with optional '0x' prefix).",
                nameof(privateKey));
        }

        try
        {
            var ecKey = new EthECKey(privateKey);
            var address = ecKey.GetPublicAddress();
            return new HyperliquidSigner(ecKey, address);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to derive wallet address from private key: {ex.Message}. Ensure the key is a valid Ethereum-compatible private key.",
                nameof(privateKey),
                ex);
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    public (string R, string S, int V) SignTypedData<TDomain>(TypedData<TDomain> typedData) where TDomain : IDomain
    {
        var signer = new Eip712TypedDataSigner();
        var encodedData = signer.EncodeTypedData(typedData);
        var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encodedData);
        var signature = _ecKey.SignAndCalculateV(hash);

        var r = "0x" + Convert.ToHexString(signature.R).ToLowerInvariant();
        var s = "0x" + Convert.ToHexString(signature.S).ToLowerInvariant();
        var v = signature.V.FirstOrDefault();

        return (r, s, (int)v);
    }
}
```

**Key change**: The constructor now stores the `EthECKey` instance rather than discarding it. The factory method `Create()` is unchanged in its public API — callers are unaffected.

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` — Current implementation (key discarded)

---

### Task 1.5: Create NonceProvider service {#task-15-create-nonceprovider-service}

Create a thread-safe nonce provider that generates monotonically increasing nonces based on UTC millisecond timestamps. Must guarantee no collisions even under concurrent access.

- **Complexity**: Medium
- **Risk Factors**: Thread safety under concurrent requests; clock skew
- **Files**:
  - `src/TradingApp.Infrastructure/Services/NonceProvider.cs` — New file: thread-safe nonce generation
  - `src/TradingApp.Application/Abstractions/Services/INonceProvider.cs` — New file: interface
- **Success**:
  - `GetNextNonce()` returns monotonically increasing values
  - Concurrent calls never return the same value
  - Values are UTC millisecond timestamps (or monotonically adjusted)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/INonceProvider.cs — new file
namespace TradingApp.Application.Abstractions.Services;

public interface INonceProvider
{
    /// <summary>
    /// Returns a unique monotonically increasing nonce based on UTC milliseconds.
    /// Thread-safe; no collisions under concurrent access.
    /// </summary>
    long GetNextNonce();
}
```

```csharp
// src/TradingApp.Infrastructure/Services/NonceProvider.cs — new file
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class NonceProvider : INonceProvider
{
    private long _lastNonce;

    public long GetNextNonce()
    {
        long currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long lastNonce;
        long newNonce;

        do
        {
            lastNonce = Interlocked.Read(ref _lastNonce);
            newNonce = Math.Max(currentMs, lastNonce + 1);
        }
        while (Interlocked.CompareExchange(ref _lastNonce, newNonce, lastNonce) != lastNonce);

        return newNonce;
    }
}
```

The lock-free `CompareExchange` loop guarantees:
- Each nonce is at least `currentMs` (tied to real time)
- Each nonce is strictly greater than the previous (monotonically increasing)
- No two concurrent callers receive the same value

Register as singleton in `Program.cs` (Phase 2, Task 2.5).

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` — Singleton infrastructure service pattern

---

### Task 1.6: Unit tests for EIP-712 signing {#task-16-unit-tests-for-eip-712-signing}

Write unit tests verifying EIP-712 hash computation, action hash computation, and end-to-end signing. Tests should verify intermediate values (domain hash, type hash, action hash) match known-good values from the Python SDK.

- **Complexity**: High
- **Risk Factors**: Need known-good reference values from Python SDK; MessagePack compatibility
- **Files**:
  - `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidEip712Tests.cs` — New file: hash computation tests
  - `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Add signing tests alongside existing address tests
- **Success**:
  - Action hash tests produce known-good SHA-256 values
  - EIP-712 signing produces valid (v, r, s) tuple
  - `HyperliquidSigner.SignTypedData` produces deterministic signatures for a given key + typed data
  - All existing signer tests still pass
- **Dependencies**: Tasks 1.2, 1.3, 1.4

#### Implementation Details

```csharp
// tests/TradingApp.Infrastructure.Tests/Services/HyperliquidEip712Tests.cs — new file
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidEip712Tests
{
    [TestMethod]
    public void GivenKnownOrderAction_WhenComputeActionHash_ThenProducesExpectedHash()
    {
        // Arrange — use a known test case verified against Python SDK
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);
        long nonce = 1716499200000L;

        // Act
        var hash = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);

        // Assert
        hash.Should().NotBeNull();
        hash.Should().HaveCount(32); // SHA-256 = 32 bytes
        // IMPLEMENTING AGENT: Before writing this test, compute the expected hash by running
        // the equivalent Python code using hyperliquid-python-sdk's action_hash() function
        // with the same inputs (assetIndex=0, isBuy=True, price="65000.0", size="0.001",
        // nonce=1716499200000). Use the Python output as the expected byte array here.
        // Example: hash.Should().BeEquivalentTo(new byte[] { 0xAB, 0xCD, ... });
    }

    [TestMethod]
    public void GivenSameInputs_WhenComputeActionHash_ThenProducesDeterministicResult()
    {
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);
        long nonce = 1716499200000L;

        var hash1 = HyperliquidEip712.ComputeActionHash(action, nonce, null);
        var hash2 = HyperliquidEip712.ComputeActionHash(action, nonce, null);

        hash1.Should().BeEquivalentTo(hash2);
    }

    [TestMethod]
    public void GivenDifferentNonces_WhenComputeActionHash_ThenProducesDifferentHashes()
    {
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        var hash1 = HyperliquidEip712.ComputeActionHash(action, 1000L, null);
        var hash2 = HyperliquidEip712.ComputeActionHash(action, 1001L, null);

        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [TestMethod]
    public void GivenTestnetConfig_WhenBuildPhantomAgentTypedData_ThenHasCorrectDomainAndTypes()
    {
        var connectionId = new byte[32];
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

        typedData.Domain.Name.Should().Be("Exchange");
        typedData.Domain.Version.Should().Be("1");
        typedData.Domain.ChainId.Should().Be(1337);
        typedData.PrimaryType.Should().Be("Agent");
    }

    [TestMethod]
    public void GivenValidParameters_WhenBuildOrderAction_ThenReturnsCorrectStructure()
    {
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: 0, isBuy: true, price: 65000.0m, size: 0.001m);

        action["type"].Should().Be("order");
        action["grouping"].Should().Be("na");
        var orders = (Dictionary<string, object>[])action["orders"];
        orders.Should().HaveCount(1);
        orders[0]["a"].Should().Be(0);
        orders[0]["b"].Should().Be(true);
    }
}
```

```csharp
// tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs — add new tests
// Add these test methods to the existing class:

[TestMethod]
public void GivenValidKey_WhenSignTypedData_ThenReturnsValidSignatureComponents()
{
    // Arrange
    var signer = HyperliquidSigner.Create(ValidPrivateKey);
    var connectionId = new byte[32]; // dummy
    var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

    // Act
    var (r, s, v) = signer.SignTypedData(typedData);

    // Assert
    r.Should().StartWith("0x").And.HaveLength(66); // 0x + 64 hex chars
    s.Should().StartWith("0x").And.HaveLength(66);
    v.Should().BeOneOf(27, 28);
}

[TestMethod]
public void GivenSameTypedData_WhenSignTwice_ThenProducesDeterministicSignature()
{
    var signer = HyperliquidSigner.Create(ValidPrivateKey);
    var connectionId = new byte[32];
    var typedData1 = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);
    var typedData2 = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet: false);

    var sig1 = signer.SignTypedData(typedData1);
    var sig2 = signer.SignTypedData(typedData2);

    sig1.Should().Be(sig2);
}
```

##### Pattern References

- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Existing signer test patterns

---

### Task 1.7: Unit tests for NonceProvider {#task-17-unit-tests-for-nonceprovider}

Write unit tests verifying monotonically increasing nonces and thread safety under concurrent access.

- **Complexity**: Low
- **Risk Factors**: Concurrent test reliability
- **Files**:
  - `tests/TradingApp.Infrastructure.Tests/Services/NonceProviderTests.cs` — New file
- **Success**:
  - Sequential calls produce increasing values
  - Concurrent calls produce unique values
- **Dependencies**: Task 1.5

#### Implementation Details

```csharp
// tests/TradingApp.Infrastructure.Tests/Services/NonceProviderTests.cs — new file
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class NonceProviderTests
{
    [TestMethod]
    public void GivenSequentialCalls_WhenGetNextNonce_ThenReturnsIncreasingValues()
    {
        var provider = new NonceProvider();

        var nonce1 = provider.GetNextNonce();
        var nonce2 = provider.GetNextNonce();
        var nonce3 = provider.GetNextNonce();

        nonce2.Should().BeGreaterThan(nonce1);
        nonce3.Should().BeGreaterThan(nonce2);
    }

    [TestMethod]
    public void GivenConcurrentCalls_WhenGetNextNonce_ThenReturnsUniqueValues()
    {
        var provider = new NonceProvider();
        var nonces = new long[1000];

        Parallel.For(0, nonces.Length, i =>
        {
            nonces[i] = provider.GetNextNonce();
        });

        nonces.Distinct().Should().HaveCount(nonces.Length);
    }

    [TestMethod]
    public void GivenNewProvider_WhenGetNextNonce_ThenReturnsRecentUtcTimestamp()
    {
        var provider = new NonceProvider();
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nonce = provider.GetNextNonce();

        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        nonce.Should().BeGreaterOrEqualTo(before);
        nonce.Should().BeLessOrEqualTo(after + 1);
    }
}
```

##### Pattern References

- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — MSTest + FluentAssertions pattern

---

### Task 1.8: Run all existing tests {#task-18-run-all-existing-tests}

Run the complete test suite to verify no regressions from the IHyperliquidSigner interface change and HyperliquidSigner refactoring.

- **Complexity**: Low
- **Risk Factors**: Interface change may break mocks if any test Setup explicitly verifies all interface members
- **Files**: None (verification only)
- **Success**:
  - All existing tests pass
  - All new Phase 1 tests pass
  - No build errors across the solution
- **Dependencies**: All previous Phase 1 tasks

Run:
```bash
dotnet test TradingApp.sln
```

---

## Phase Success Criteria

- `HyperliquidSigner.SignTypedData` produces valid EIP-712 signatures with retained `EthECKey`
- `HyperliquidEip712.ComputeActionHash` computes SHA-256 hash of MessagePack-serialized action + nonce
- `HyperliquidEip712.BuildPhantomAgentTypedData` constructs correct testnet EIP-712 structure
- `NonceProvider.GetNextNonce` returns monotonically increasing values under concurrent access
- All new unit tests pass
- All existing tests pass (no regressions)
- Solution builds without errors
