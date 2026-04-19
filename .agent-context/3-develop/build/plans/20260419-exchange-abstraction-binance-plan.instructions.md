---
applyTo: ".agent-context/3-develop/build/changes/20260419-exchange-abstraction-binance-changes.md"
currentAgent: "Implementation Planner"
agentStartedAt: "2026-04-19T18:58:29Z"
status: "planned"
lastUpdated: "2026-04-19T19:46:47Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Exchange Abstraction For Hyperliquid And Binance

## Overview

Introduce a capability-based exchange abstraction so the platform can preserve the current Hyperliquid-first live trading flow while enabling incremental Binance support without hard-wiring more `IHyperliquid*` dependencies into the Application layer.

The immediate goal is not to replace all exchange-specific code in one pass. The goal is to establish canonical domain types for market identity and exchange selection, create stable Application-layer contracts for exchange capabilities, adapt Hyperliquid behind those contracts first, and then plug Binance into the same seams in phases.

## Objectives

- Preserve the current live trading behavior for Hyperliquid
- Preserve the worker-side private key custody boundary
- Preserve current DCA-on-perps behavior for Hyperliquid
- Avoid a monolithic god interface for all exchange behavior
- Introduce capability-based interfaces in `TradePilot.Application`
- Migrate Application services away from direct `IHyperliquid*` dependencies where practical
- Make exchange differences explicit through capabilities instead of implicit rules
- Introduce a canonical `TradingPair` value object as the single internal market identifier
- Create a low-risk path for adding Binance as a second exchange

### Discovery References

- `.agent-context/0-knowledge/02-hyperliquid-integration.md`
- `.agent-context/0-knowledge/03-infrastructure-architecture.md`
- `.agent-context/0-knowledge/06-project-structure.md`
- `.agent-context/0-knowledge/10-architecture-decisions.md`
- `src/TradePilot.Application/Abstractions/Services/IExecutionEngine.cs`
- `src/TradePilot.Application/Trading/Services/LiveMarketContextBuilder.cs`
- `src/TradePilot.Application/Trading/Services/StateRecoveryService.cs`
- `src/TradePilot.Api/Services/HyperliquidExecutionEngine.cs`
- `src/TradePilot.Application/Abstractions/Services/IBinanceFuturesRestClient.cs`
- `src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs`
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs`
- `src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs`
- `src/TradePilot.Application/StrategyAuthoring/Models/AssetType.cs`
- `src/TradePilot.Application/Trading/Models/OrderRequest.cs`
- `src/TradePilot.Domain/Entities/Candle.cs` — `Symbol` (free string), `Source` (free string)
- `src/TradePilot.Domain/Entities/UserWalletAddress.cs` — `Exchange` (free string)
- `src/TradePilot.Domain/Entities/GridCycle.cs` — `Symbol` (free string)
- `src/TradePilot.Domain/Entities/LiveOrder.cs` — `Symbol` (free string)
- `src/TradePilot.Domain/Entities/LiveFill.cs` — `Symbol` (free string)
- `src/TradePilot.Domain/Subscriptions/TierFeaturePolicy.cs` — `AllowedAssets` (free strings)

### Project Patterns

- Keep exchange-neutral contracts in `src/TradePilot.Application/Abstractions/Services/`
- Keep exchange-specific implementations in `src/TradePilot.Infrastructure/` or host-specific adapters in `src/TradePilot.Api/`
- Preserve the existing execution seam in `IExecutionEngine` initially unless a rename is worth the churn
- Model venue differences through explicit capabilities rather than hidden conditional logic

## Recommended Target Architecture

### Canonical Market Identity

The platform needs a single internal representation for what is being traded. Today, symbols are free strings scattered across entities (`Candle.Symbol = "BTC-PERP"`, `OrderRequest.Symbol = "BTC"`, `GridCycle.Symbol = "BTC-PERP"`), and each exchange mapper converts independently without a shared canonical form.

#### `TradingPair` Value Object (Domain)

A `TradingPair` captures the base asset, quote currency, and product type in one immutable value:

```csharp
// TradePilot.Domain/ValueObjects/TradingPair.cs
public sealed record TradingPair
{
    public string Base { get; init; }          // "BTC", "ETH", "SOL"
    public string Quote { get; init; }         // "USD", "USDT"
    public AssetType ProductType { get; init; } // Perp, Spot

    // Canonical string format: "BTC/USD:PERP", "ETH/USDT:SPOT"
    public string Canonical => $"{Base}/{Quote}:{ProductType}";

    public static TradingPair Parse(string canonical) { /* parse from canonical string */ }
}
```

This resolves:
- **Ambiguity**: `"BTC"` alone doesn't tell you spot vs perp — `TradingPair` makes it explicit
- **Mapping direction**: Each exchange mapper converts to/from `TradingPair`, not from free strings
- **Spot support**: Hyperliquid is perps-only, Binance supports spot. The `ProductType` field makes this a first-class concern rather than an implicit assumption

#### `Exchange` Enum (Domain)

Replace the free-text `string` used in `UserWalletAddress.Exchange` and `Candle.Source` with a domain enum:

```csharp
// TradePilot.Domain/Enums/Exchange.cs
public enum Exchange { Hyperliquid, Binance }
```

#### `AssetType` Relocation

`AssetType` currently lives in `TradePilot.Application/StrategyAuthoring/Models/` but is a domain concept referenced by `TradingPair`. Move it to `TradePilot.Domain/Enums/AssetType.cs`.

#### Current Symbol Formats By Exchange

| Application canonical | Hyperliquid API | Binance Futures API | Notes |
|---|---|---|---|
| `BTC/USD:PERP` | `BTC` (bare coin) | `BTCUSDT` | Different quote conventions |
| `ETH/USD:PERP` | `ETH` | `ETHUSDT` | |
| `BTC/USDT:SPOT` | N/A (not supported) | `BTCUSDT` | Spot not available on Hyperliquid |

### Capability Interfaces

- `IExecutionEngine`
  - Keep as the execution capability interface for now to minimize churn
  - Covers place order, cancel order, cancel all, trigger orders, leverage

- `IExchangeMarketMetadataProvider`
  - Covers market info, max leverage, tick size, lot size — the metadata queries needed by `LiveMarketContextBuilder`
  - Deliberately separate from historical data fetching to avoid a "medium god interface"

- `IExchangeHistoricalDataClient`
  - Covers candle snapshots, funding rates, and bulk historical data queries used by ingestion and backtesting

- `IExchangeAccountClient`
  - Covers fills, balances, positions, open orders, and account-state queries used by recovery and dashboard flows

- `IExchangeCapabilities`
  - Describes supported asset classes, product types, and exchange features
  - Answers questions like `Supports(TradingPair pair)` to validate at configuration time

- `IExchangeSymbolMapper`
  - Handles bidirectional conversion between `TradingPair` and exchange-native symbols

#### Stream Interfaces (Deferred)

The following interfaces are aspirational and should **not** be implemented until two exchanges are actively wired:

- `IExchangePublicStream` — public trade and candle subscriptions
- `IExchangeUserEventStream` — wallet/account-scoped fill and order update events

WebSocket lifecycle (connect, disconnect, reconnect, resubscribe) varies significantly across exchanges. These should wait until concrete requirements emerge from a second venue.

### Optional Thin Aggregator

- `IExchangeClient`
  - Optional composition root only — may be redundant if .NET keyed services handle resolution
  - Should expose capability services and metadata, not every raw API method

Suggested shape:

```csharp
public interface IExchangeClient
{
    Exchange Exchange { get; }
    IExecutionEngine Execution { get; }
    IExchangeMarketMetadataProvider MarketMetadata { get; }
    IExchangeAccountClient Account { get; }
    IExchangeCapabilities Capabilities { get; }
    IExchangeSymbolMapper Symbols { get; }
}
```

> **Decision**: Defer creating `IExchangeClient` until Phase 4. .NET keyed services (`[FromKeyedServices]`) may make it unnecessary. Evaluate when DI composition is implemented.

### Symbol Mapper Contract

The mapper must handle bidirectional conversion and be a proper injectable service (not a static class):

```csharp
public interface IExchangeSymbolMapper
{
    string ToExchangeSymbol(TradingPair pair);           // "BTC/USD:PERP" → "BTC" (HL) or "BTCUSDT" (Binance)
    TradingPair FromExchangeSymbol(string exchangeSymbol); // reverse
    bool CanMap(TradingPair pair);                        // does this exchange support this pair?
}
```

### Capability Model

The capability model makes venue differences explicit. The initial shape should cover:

- `SupportedProductTypes` → `IReadOnlySet<AssetType>` (Hyperliquid: `{Perp}`, Binance: `{Perp, Spot}`)
- `SupportsLeverage`
- `SupportsTriggerOrders`
- `SupportsReduceOnly`
- `SupportsPublicTradesStream`
- `SupportsUserEventStream`
- `SupportsPerUserNetworkRouting`
- `SupportedOrderTypes`
- `SupportedTimeframes`
- `Supports(TradingPair pair)` → validates whether a specific pair is tradeable

### Authentication Model — Non-Goal

Authentication stays **exchange-specific** and is never abstracted behind a neutral interface:

- **Hyperliquid**: EVM wallet signing (EIP-712), per-user private keys, `ISignerProvider` / `IHyperliquidSigner`
- **Binance**: HMAC-SHA256 API key + secret pairs

Attempting to unify these would be a leaky abstraction. `ISignerProvider` remains Hyperliquid-specific infrastructure.

## Phase Plan

### [ ] Phase 0: Domain Foundations

**Complexity**: Low | **Risk**: Low

- [ ] Task 0.1: Add `TradingPair` value object to `src/TradePilot.Domain/ValueObjects/` with `Base`, `Quote`, `ProductType`, canonical string format (`BTC/USD:PERP`), and `Parse()` factory
- [ ] Task 0.2: Move `AssetType` enum from `src/TradePilot.Application/StrategyAuthoring/Models/` to `src/TradePilot.Domain/Enums/` and update all references
- [ ] Task 0.3: Add `Exchange` enum to `src/TradePilot.Domain/Enums/` with values `Hyperliquid`, `Binance`
- [ ] Task 0.4: Remove redundant `IBinanceCandleIngestionService` — it has the identical signature as the neutral `ICandleIngestionService` and should be consolidated
- [ ] Task 0.5: Unit tests for `TradingPair` parsing, canonical format round-trip, and equality

Acceptance criteria:

- `TradingPair` compiles and is usable from Domain and Application layers
- `AssetType` is a Domain enum, all existing references update cleanly
- `Exchange` enum exists but does **not** yet replace free strings in entities (that migration is deferred to avoid schema churn in Phase 0)
- Existing tests pass with no behavior change

> **Note**: Migrating `Candle.Source`, `UserWalletAddress.Exchange`, and entity `Symbol` fields to use `Exchange` enum / `TradingPair` is deferred. These are database-backed columns requiring migration scripts and should be planned as a separate incremental step after the abstractions prove stable.

### [ ] Phase 1: Introduce Neutral Contracts

**Complexity**: Medium | **Risk**: Low

- [ ] Task 1.1: Add `IExchangeMarketMetadataProvider` to `src/TradePilot.Application/Abstractions/Services/` — covers market info, max leverage, tick size
- [ ] Task 1.2: Add `IExchangeHistoricalDataClient` to `src/TradePilot.Application/Abstractions/Services/` — covers candle snapshots, funding rates for ingestion/backtesting
- [ ] Task 1.3: Add `IExchangeAccountClient` to `src/TradePilot.Application/Abstractions/Services/`
- [ ] Task 1.4: Add `IExchangeCapabilities` plus a simple immutable capabilities record using `TradingPair` and `AssetType`
- [ ] Task 1.5: Add `IExchangeSymbolMapper` using `TradingPair` for bidirectional conversion
- [ ] Task 1.6: Decide whether `IExecutionEngine` stays in place or is aliased/renamed in a later phase

Acceptance criteria:

- New Application-layer abstractions compile without changing runtime behavior
- Interfaces use `TradingPair` as the market identifier where applicable
- No existing Hyperliquid integration is removed in this phase
- The abstractions are generic enough for Hyperliquid and Binance without exposing signing details

### [ ] Phase 2: Hyperliquid Adapters And Pre-Migration Cleanup

**Complexity**: Medium | **Risk**: Low

- [ ] Task 2.1: Refactor `StateRecoveryService` to use `IHyperliquidAccountService.GetOpenOrdersAsync()` instead of raw `PostInfoAsync<JsonElement>(new { type = "openOrders", user = ... })` — this is a Hyperliquid-internal cleanup that makes Phase 3 migration straightforward
- [ ] Task 2.2: Create a Hyperliquid market-metadata adapter implementing `IExchangeMarketMetadataProvider` over `IHyperliquidRestClient.GetMarketInfoAsync`
- [ ] Task 2.3: Create a Hyperliquid account adapter implementing `IExchangeAccountClient` over `IHyperliquidAccountService`
- [ ] Task 2.4: Refactor `HyperliquidAssetMapper` from a static class to an injectable `IExchangeSymbolMapper` implementation using `TradingPair`
- [ ] Task 2.5: Add a Hyperliquid capabilities implementation capturing current venue support (`SupportedProductTypes = {Perp}`, leverage = true, etc.)
- [ ] Task 2.6: Similarly refactor `BinanceAssetMapper` from a static class to an injectable `IExchangeSymbolMapper` implementation

Acceptance criteria:

- Hyperliquid implements the new contracts without changing existing API or Worker live trading behavior
- `StateRecoveryService` no longer uses raw `PostInfoAsync` for open orders
- Hyperliquid-specific signing remains behind existing infrastructure and worker boundaries
- Symbol mapping and asset-type behavior remain unchanged for current live flows
- Both mappers are injectable and testable, not static

### [ ] Phase 3: Migrate Application Consumers

**Complexity**: High | **Risk**: Medium

- [ ] Task 3.1: Refactor `LiveMarketContextBuilder` to depend on `IExchangeMarketMetadataProvider` instead of `IHyperliquidRestClient` (only used for `GetMarketInfoAsync` / max leverage)
- [ ] Task 3.2: Refactor `StateRecoveryService` to depend on `IExchangeAccountClient` instead of `IHyperliquidRestClient` (now clean after Phase 2 cleanup)
- [ ] Task 3.3: Review health checks and market-data queries for direct `IHyperliquid*` dependencies and migrate the read-side ones that are truly exchange-neutral
- [ ] Task 3.4: Keep any Hyperliquid-only orchestration in place where a generic abstraction would be premature

Acceptance criteria:

- `LiveMarketContextBuilder` no longer depends directly on `IHyperliquidRestClient`
- `StateRecoveryService` no longer depends directly on `IHyperliquidRestClient`
- Targeted trading tests continue to pass with no runtime behavior change

### [ ] Phase 4: Runtime Selection And DI Composition

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 4.1: Use .NET keyed services (`[FromKeyedServices]`) to register exchange implementations keyed by `Exchange` enum
- [ ] Task 4.2: Register Hyperliquid as the default exchange implementation
- [ ] Task 4.3: Add a resolver/factory for exchange capabilities required by a session or request
- [ ] Task 4.4: Preserve per-user network routing for Hyperliquid without forcing the same behavior on Binance
- [ ] Task 4.5: Evaluate whether `IExchangeClient` aggregator adds value or is redundant given keyed DI — decide and document

Acceptance criteria:

- Strategy configs specifying `Exchange = Hyperliquid` continue to run unchanged
- DI can resolve exchange-neutral services based on the `Exchange` enum value
- Hyperliquid network routing remains per-user where currently supported
- DI composition is explicit and testable

### [ ] Phase 5: Add Binance Through The New Seams (Future)

**Complexity**: High | **Risk**: Medium

- [ ] Task 5.1: Add a Binance market-metadata adapter implementing `IExchangeMarketMetadataProvider`
- [ ] Task 5.2: Add a Binance account adapter over `IBinanceFuturesRestClient` (or a new Binance account client)
- [ ] Task 5.3: Add Binance capabilities implementation (`SupportedProductTypes = {Perp, Spot}`, etc.)
- [ ] Task 5.4: Register Binance implementations as keyed services alongside Hyperliquid
- [ ] Task 5.5: Decide whether the first Binance milestone is historical/read-only or live execution capable
- [ ] Task 5.6: If live trading is in scope, define Binance execution contracts behind the same capability seams

Acceptance criteria:

- Binance can be registered without changing Application-layer consumers
- Binance support is constrained to the capabilities actually implemented
- Unsupported behaviors (e.g. Binance spot on a perps-only strategy) are surfaced through `IExchangeCapabilities` at configuration time rather than hidden runtime failures

## First Concrete Files To Change

### [ ] Phase 0 initial file set

- [ ] `src/TradePilot.Domain/ValueObjects/TradingPair.cs` (new)
- [ ] `src/TradePilot.Domain/Enums/Exchange.cs` (new)
- [ ] `src/TradePilot.Domain/Enums/AssetType.cs` (moved from Application)
- [ ] `src/TradePilot.Application/StrategyAuthoring/Models/AssetType.cs` (deleted, replaced by Domain enum)
- [ ] `src/TradePilot.Application/Trading/Models/OrderRequest.cs` (update `AssetType` namespace)
- [ ] `src/TradePilot.Application/Abstractions/Services/IBinanceCandleIngestionService.cs` (remove, consolidate with `ICandleIngestionService`)
- [ ] `tests/TradePilot.Domain.Tests/ValueObjects/TradingPairTests.cs` (new)

### [ ] Phase 1 initial file set

- [ ] `src/TradePilot.Application/Abstractions/Services/IExchangeMarketMetadataProvider.cs` (new)
- [ ] `src/TradePilot.Application/Abstractions/Services/IExchangeHistoricalDataClient.cs` (new)
- [ ] `src/TradePilot.Application/Abstractions/Services/IExchangeAccountClient.cs` (new)
- [ ] `src/TradePilot.Application/Abstractions/Services/IExchangeCapabilities.cs` (new)
- [ ] `src/TradePilot.Application/Abstractions/Services/IExchangeSymbolMapper.cs` (new)

### [ ] Phase 2 initial file set

- [ ] `src/TradePilot.Application/Trading/Services/StateRecoveryService.cs` (cleanup: replace raw `PostInfoAsync` with `IHyperliquidAccountService`)
- [ ] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidMarketMetadataProvider.cs` (new adapter)
- [ ] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAccountAdapter.cs` (new adapter)
- [ ] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` (refactor: static → injectable `IExchangeSymbolMapper`)
- [ ] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs` (new)
- [ ] `src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs` (refactor: static → injectable `IExchangeSymbolMapper`)

## Risks And Design Concerns

### Symbol Mapping Risk

- Hyperliquid uses bare coin names (`BTC`), Binance uses pair symbols (`BTCUSDT`)
- Both current mappers are static classes with hardcoded dictionaries — refactoring to injectable services requires updating all call sites
- The canonical `TradingPair` format (`BTC/USD:PERP`) is new — existing entity `Symbol` fields remain free strings until a dedicated migration phase
- Entity migration (changing `Candle.Symbol`, `GridCycle.Symbol`, etc. to use `TradingPair.Canonical`) requires database migration scripts and should be deferred

### Spot vs Perps Risk

- All current strategy logic assumes perpetual contracts (DCA on perps, leverage, funding rates)
- Adding spot support is not just a symbol mapping concern — it changes position sizing, margin, and P&L calculation
- The `TradingPair.ProductType` field and `IExchangeCapabilities.SupportedProductTypes` make this boundary explicit, but strategy logic for spot would be a separate feature

### Trigger Order Risk

- Hyperliquid trigger order support is exchange-native and already part of the current live model
- Binance trigger order semantics may differ significantly
- Trigger order capabilities should be exposed explicitly, not assumed for all venues

### State Recovery Risk

- `StateRecoveryService` currently uses raw `PostInfoAsync<JsonElement>(new { type = "openOrders", user = ... })` — a Hyperliquid-specific JSON-RPC protocol detail
- Phase 2 Task 2.1 must refactor this to use `IHyperliquidAccountService.GetOpenOrdersAsync()` before the migration to neutral interfaces, otherwise the Phase 3 swap would require re-implementing protocol-level details in an exchange-neutral way
- The account abstraction must normalize fills, positions, and open orders without losing important detail used by recovery logic

### WebSocket Lifecycle Risk

- Generic stream abstractions are useful, but reconnect/resubscribe semantics may differ widely across exchanges
- REST/read and execution abstractions should be prioritized before deep websocket generalization

### DI And Runtime Selection Risk

- Current registrations bind `IExecutionEngine` directly to Hyperliquid implementations
- The new selection model must work for API-hosted and Worker-hosted execution paths
- Exchange selection should not leak into unrelated services via ad hoc `if (exchange == ...)` checks
- .NET keyed services (`[FromKeyedServices]`) are the recommended mechanism for exchange-keyed resolution — this avoids custom factory patterns

### Custody And Signing Risk

- Worker-side key custody is a core architectural boundary
- Hyperliquid signing and runtime key management must remain infrastructure-specific and must not be forced into the generic exchange abstractions

## Testing Strategy

### Unit Tests

- Add `TradingPair` round-trip parsing tests (canonical format → parse → canonical format)
- Add contract-focused tests for Hyperliquid adapters implementing the new market-metadata and account abstractions
- Add tests for `IExchangeSymbolMapper` implementations: `TradingPair` → exchange symbol → `TradingPair` round-trips for both Hyperliquid and Binance mappers
- Add tests for capability declarations used by validation/runtime selection
- Add tests for `IExchangeCapabilities.Supports(TradingPair)` — e.g., Hyperliquid rejects spot pairs

### Application Tests

- Add tests proving `LiveMarketContextBuilder` works with a fake `IExchangeMarketDataClient`
- Add tests proving `StateRecoveryService` works with a fake `IExchangeAccountClient`
- Keep current DCA and live position manager tests green throughout the migration

### Composition Tests

- Add DI tests or focused host tests proving the right exchange services resolve for Hyperliquid strategies
- Add future tests for Binance registration without modifying Application services

## Success Criteria

- Application services stop depending directly on Hyperliquid contracts where exchange-neutral behavior is sufficient
- Hyperliquid remains the current live trading venue with no behavior regression
- Exchange capabilities are explicit and testable
- Binance can be introduced behind the same market-data and account seams
- The worker key-custody model remains intact
- Hyperliquid DCA on perps remains unchanged

## Open Questions

- Should Binance initial support be historical/read-only or live-execution capable?
- Should `IExecutionEngine` be renamed, or is keeping it stable the better migration path?
- Is a thin `IExchangeClient` aggregator useful enough to justify the extra layer now, or should keyed DI handle resolution without it?
- When should entity `Symbol` fields migrate from free strings to `TradingPair.Canonical`? (requires DB migration — recommended as a separate follow-up after abstractions prove stable)
- What is the canonical quote currency for the application? (Candidates: `USD` for Hyperliquid perps, `USDT` for Binance. May need normalization rules in `TradingPair`)

## Recommendation

Proceed with capability interfaces first and keep `IExecutionEngine` as the current execution seam for the first migration wave. Do not introduce a large god-interface `IExchangeClient`. If a top-level exchange root is added, keep it as a thin aggregator over smaller capabilities.

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-19T18:58:29Z | 2026-04-19T18:58:29Z |