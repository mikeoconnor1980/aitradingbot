<!-- markdownlint-disable-file -->

# Task Details: Binance Integration Review Round 2 Fixes

## Phase 2: Exchange-Authoritative Max Leverage via Leverage Bracket API

## Standards and Knowledge References

- **C# Standards** (`.github/instructions/csharp.instructions.md`): Use `sealed` classes with `init`-only properties. `[JsonPropertyName]` on every property for explicit mapping.
- **Testing Standards** (`.github/instructions/testing.instructions.md`): MSTest, Moq, FluentAssertions ≤ v6. Given_When_Then naming.
- **Exchange Abstraction** (`.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`): `IExchangeSymbolMetadataProvider` exposes `MaxLeverage` from `ExchangeSymbolMetadata`. `BinanceSymbolMetadataProvider` bridges `BinanceExchangeSymbolMetadata` → `ExchangeSymbolMetadata`.
- **Binance Integration** (`.agent-context/0-knowledge/23-binance-integration.md`): `BinanceExchangeInfoCache` stores exchange metadata. Currently uses hardcoded `MaxLeverageByAsset` for BTC (125x) and ETH (125x); all others default to 25x.

## Design References

- **Binance API**: `GET /fapi/v1/leverageBracket` (authenticated) returns an array of `{ symbol, brackets: [{ bracket, initialLeverage, notionalCap, notionalFloor, maintMarginRatio, cum }] }`. Bracket 1 (first in array) has the highest `initialLeverage` = max leverage for that symbol.
- **Existing response model pattern**: All snapshot models in `IBinanceFuturesAuthClient.cs` use `sealed class`, `init`-only properties, `[JsonPropertyName]` attributes.
- **`SendReadOnlyListAsync<T>`**: Thin wrapper over `SendAsync<List<T>>` — use for endpoints returning JSON arrays.
- **DI constraint**: `BinanceExchangeInfoCache` currently only depends on `IHttpClientFactory` (public API). Adding `IBinanceFuturesAuthClient` is a new dependency that requires credentials. The leverage bracket fetch MUST be optional — if the auth client is null or the call fails, fall back to conservative defaults.

### Task 2.1: Add leverage bracket response models and interface method {#task-21-add-leverage-bracket-models}

Add the `BinanceLeverageBracketResponse` and `BinanceLeverageBracket` models to `IBinanceFuturesAuthClient.cs` and add the `GetLeverageBracketsAsync` method to the interface and implementation.

- **Complexity**: Medium
- **Risk Factors**: Must match Binance's actual JSON property names exactly. `notionalCap`/`notionalFloor` are returned as `long` in some API docs and `decimal` in others — use `long` per the more common pattern.
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/IBinanceFuturesAuthClient.cs` — Add models + interface method
  - `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs` — Add implementation
- **Success**:
  - `BinanceLeverageBracketResponse` and `BinanceLeverageBracket` models defined with correct JSON mapping
  - Interface method `GetLeverageBracketsAsync` declared
  - Implementation calls `GET /fapi/v1/leverageBracket` via `SendReadOnlyListAsync`
- **Dependencies**:
  - None — independent of Phase 1

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/IBinanceFuturesAuthClient.cs — modification
// Add to interface (after existing method declarations):

Task<IReadOnlyList<BinanceLeverageBracketResponse>> GetLeverageBracketsAsync(
    CancellationToken cancellationToken = default);

// Add models at bottom of file (after existing model classes):

public sealed class BinanceLeverageBracketResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("brackets")]
    public IReadOnlyList<BinanceLeverageBracket> Brackets { get; init; } = [];
}

public sealed class BinanceLeverageBracket
{
    [JsonPropertyName("bracket")]
    public int Bracket { get; init; }

    [JsonPropertyName("initialLeverage")]
    public int InitialLeverage { get; init; }

    [JsonPropertyName("notionalCap")]
    public long NotionalCap { get; init; }

    [JsonPropertyName("notionalFloor")]
    public long NotionalFloor { get; init; }

    [JsonPropertyName("maintMarginRatio")]
    public decimal MaintMarginRatio { get; init; }

    [JsonPropertyName("cum")]
    public decimal Cum { get; init; }
}
```

```csharp
// src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs — modification
// Add method implementation (follow existing GetOpenOrdersAsync pattern):

public async Task<IReadOnlyList<BinanceLeverageBracketResponse>> GetLeverageBracketsAsync(
    CancellationToken cancellationToken = default)
{
    return await SendReadOnlyListAsync<BinanceLeverageBracketResponse>(
        HttpMethod.Get,
        "/fapi/v1/leverageBracket",
        queryParameters: null,
        cancellationToken);
}
```

##### Pattern References

- `GetOpenOrdersAsync` in `BinanceFuturesAuthClient.cs` — same pattern: optional query param, calls `SendReadOnlyListAsync<T>`, returns `IReadOnlyList<T>`.
- `BinanceExchangeInfoSymbol` + `BinanceExchangeFilter` — precedent for nested list model (`IReadOnlyList<T>` child property).
- All models in `IBinanceFuturesAuthClient.cs` — sealed class, init-only, JsonPropertyName on every property.

---

### Task 2.2: Integrate leverage brackets into BinanceExchangeInfoCache {#task-22-integrate-leverage-brackets}

Inject `IBinanceFuturesAuthClient` as an optional dependency and fetch leverage brackets during `EnsureCacheAsync`. Replace the hardcoded `MaxLeverageByAsset` with exchange-authoritative values.

- **Complexity**: Medium
- **Risk Factors**: The cache is potentially used by public-only consumers (historical data ingestion). The auth client dependency MUST be nullable/optional. If leverage bracket fetch fails, fall back to conservative 25x for all assets.
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs` — Inject auth client, fetch brackets, remove static dictionary
  - `src/TradePilot.Application/Abstractions/Services/IBinanceExchangeInfoCache.cs` — No change needed (metadata record changes are in Phase 4)
- **Success**:
  - `IBinanceFuturesAuthClient?` injected as nullable constructor parameter
  - During `EnsureCacheAsync`, after fetching exchange info, calls `GetLeverageBracketsAsync()` if auth client available
  - Max leverage extracted from bracket 1 (`Brackets[0].InitialLeverage`) for each supported symbol
  - Hardcoded `MaxLeverageByAsset` dictionary removed
  - Graceful fallback (25x) if auth client is null or bracket fetch fails
- **Dependencies**:
  - Task 2.1 (models and interface method must exist)

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs — modification

// 1. Remove the static MaxLeverageByAsset dictionary (lines 12-16):
// DELETE:
// private static readonly IReadOnlyDictionary<string, int> MaxLeverageByAsset = ...

// 2. Add new constructor parameter:
// BEFORE (approximate):
public BinanceExchangeInfoCache(IHttpClientFactory httpClientFactory, ILogger<BinanceExchangeInfoCache> logger)
// AFTER:
public BinanceExchangeInfoCache(
    IHttpClientFactory httpClientFactory,
    ILogger<BinanceExchangeInfoCache> logger,
    IBinanceFuturesAuthClient? authClient = null)
{
    _httpClientFactory = httpClientFactory;
    _logger = logger;
    _authClient = authClient;
}

// Add field:
private readonly IBinanceFuturesAuthClient? _authClient;

// 3. In EnsureCacheAsync, after fecthing exchangeInfo, add leverage bracket fetch:
// Add after the exchangeInfo HTTP call and before the symbol loop:

var leverageBrackets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
if (_authClient is not null)
{
    try
    {
        var brackets = await _authClient.GetLeverageBracketsAsync(cancellationToken);
        foreach (var entry in brackets)
        {
            if (entry.Brackets.Count > 0)
            {
                var asset = BinanceAssetMapper.NormalizeSymbol(entry.Symbol);
                leverageBrackets[asset] = entry.Brackets[0].InitialLeverage;
            }
        }
        _logger.LogInformation("Fetched leverage brackets for {Count} symbols from Binance.", leverageBrackets.Count);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to fetch leverage brackets from Binance. Using default max leverage (25x).");
    }
}

// 4. In the symbol loop where BinanceExchangeSymbolMetadata is constructed:
// BEFORE:
// MaxLeverageByAsset.TryGetValue(asset, out var maxLeverage) ? maxLeverage : 25
// AFTER:
leverageBrackets.TryGetValue(asset, out var maxLeverage) ? maxLeverage : 25
```

**DI registration note**: `BinanceExchangeInfoCache` must be registered where `IBinanceFuturesAuthClient` is resolvable. Since the constructor parameter is nullable with a default, the DI container will pass `null` if the auth client isn't registered (e.g., for historical data-only scenarios). Verify the DI registrations in `Program.cs` — the cache is likely already registered after the auth client.

##### Pattern References

- `BinanceExchangeInfoCache.EnsureCacheAsync` — existing method that fetches `exchangeInfo` from the public API. The leverage bracket fetch slots in after the HTTP call and before the metadata construction loop.
- `BinanceAssetMapper.NormalizeSymbol` — converts `"BTCUSDT"` → `"BTC"`, same as used in the cache's existing symbol normalization.
- Constructor injection with nullable — follows C# convention for optional dependencies. The `= null` default allows the DI container to skip the parameter when no implementation is registered.

---

### Task 2.3: Add unit tests for leverage bracket integration {#task-23-add-leverage-bracket-tests}

Add tests verifying that the cache returns exchange-authoritative max leverage values, and that failures fall back to defaults.

- **Complexity**: Medium
- **Risk Factors**: `BinanceExchangeInfoCacheTests` uses custom `TestHttpClientFactory` and `SequenceHttpMessageHandler`. Need to add `IBinanceFuturesAuthClient` mock to the test setup. May need to update existing test constructor calls.
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExchangeInfoCacheTests.cs` — Add leverage bracket tests and update constructor setup
- **Success**:
  - Test: when auth client returns brackets, `MaxLeverage` reflects exchange-authoritative values
  - Test: when auth client is null (no auth), `MaxLeverage` defaults to 25
  - Test: when bracket fetch throws, `MaxLeverage` defaults to 25
  - All existing cache tests continue to pass
- **Dependencies**:
  - Task 2.2 (cache constructor and EnsureCacheAsync changes must be in place)

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceExchangeInfoCacheTests.cs — modification

// 1. Add mock field in test class:
private Mock<IBinanceFuturesAuthClient> _authClientMock = null!;

// 2. In [TestInitialize], create mock and pass to SUT constructor:
_authClientMock = new Mock<IBinanceFuturesAuthClient>(MockBehavior.Strict);
// Update existing SUT construction to pass auth client:
_sut = new BinanceExchangeInfoCache(_httpClientFactory, _logger, _authClientMock.Object);

// 3. In existing tests, add default leverage bracket setup if needed:
_authClientMock
    .Setup(c => c.GetLeverageBracketsAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(Array.Empty<BinanceLeverageBracketResponse>());

// 4. New test — exchange-authoritative leverage:
[TestMethod]
public async Task GivenBracketsAvailable_WhenGetSymbolAsync_ThenMaxLeverageIsExchangeAuthoritative()
{
    // Arrange
    var brackets = new[]
    {
        new BinanceLeverageBracketResponse
        {
            Symbol = "BTCUSDT",
            Brackets = [new BinanceLeverageBracket { Bracket = 1, InitialLeverage = 125 }]
        },
        new BinanceLeverageBracketResponse
        {
            Symbol = "SOLUSDT",
            Brackets = [new BinanceLeverageBracket { Bracket = 1, InitialLeverage = 50 }]
        }
    };
    _authClientMock
        .Setup(c => c.GetLeverageBracketsAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(brackets);

    // Setup exchange info HTTP response with BTC + SOL symbols
    // (use existing CreateExchangeInfoResponse helper)

    // Act
    var btcMeta = await _sut.GetSymbolAsync("BTC", CancellationToken.None);
    var solMeta = await _sut.GetSymbolAsync("SOL", CancellationToken.None);

    // Assert
    btcMeta.MaxLeverage.Should().Be(125);
    solMeta.MaxLeverage.Should().Be(50);
}

// 5. New test — bracket fetch failure falls back to 25:
[TestMethod]
public async Task GivenBracketFetchFailure_WhenGetSymbolAsync_ThenMaxLeverageDefaultsTo25()
{
    // Arrange
    _authClientMock
        .Setup(c => c.GetLeverageBracketsAsync(It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HttpRequestException("Unauthorized"));

    // Setup exchange info HTTP response

    // Act
    var metadata = await _sut.GetSymbolAsync("BTC", CancellationToken.None);

    // Assert
    metadata.MaxLeverage.Should().Be(25);
}

// 6. New test — null auth client falls back to 25:
[TestMethod]
public async Task GivenNoAuthClient_WhenGetSymbolAsync_ThenMaxLeverageDefaultsTo25()
{
    // Arrange — construct SUT with null auth client
    var sutNoAuth = new BinanceExchangeInfoCache(_httpClientFactory, _logger, authClient: null);

    // Setup exchange info HTTP response

    // Act
    var metadata = await sutNoAuth.GetSymbolAsync("BTC", CancellationToken.None);

    // Assert
    metadata.MaxLeverage.Should().Be(25);
}
```

##### Pattern References

- Existing `BinanceExchangeInfoCacheTests` — uses `TestHttpClientFactory` + `SequenceHttpMessageHandler` for public API mocking, `CreateExchangeInfoResponse` for fixture data.
- Mock setup style matches existing strict mocks in the test project.

---

### Task 2.4: Build and verify all tests pass {#task-24-build-and-verify}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Existing cache tests may need `GetLeverageBracketsAsync` setup added if using strict mock. DI resolution in integration tests (if any) may need auth client registration.
- **Files**:
  - Solution level — all projects
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test TradePilot.sln` — all tests pass
  - No new warnings in modified files
- **Dependencies**:
  - Tasks 2.1, 2.2, 2.3

## Phase Success Criteria

- `MaxLeverageByAsset` hardcoded dictionary is removed from `BinanceExchangeInfoCache`
- Leverage brackets are fetched from `GET /fapi/v1/leverageBracket` during cache refresh
- `BinanceExchangeSymbolMetadata.MaxLeverage` reflects exchange-authoritative values for all supported assets
- Graceful fallback to 25x when auth client is unavailable or bracket fetch fails
- New tests verify happy path, failure path, and null-auth-client path
- All existing tests continue to pass
- Solution builds without errors
