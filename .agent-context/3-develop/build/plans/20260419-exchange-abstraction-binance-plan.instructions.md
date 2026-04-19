---
applyTo: ".agent-context/3-develop/build/changes/20260419-exchange-abstraction-binance-changes.md"
currentAgent: "Plan Implementer"
agentStartedAt: "2026-04-19T20:18:04Z"
status: "in-progress"
lastUpdated: "2026-04-19T20:18:04Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Exchange Abstraction For Hyperliquid And Binance

## Overview

Introduce a capability-based exchange abstraction so the platform can preserve the current Hyperliquid-first live trading flow while enabling incremental Binance support without hard-wiring more `IHyperliquid*` dependencies into the Application layer.

The immediate goal is not to replace all exchange-specific code in one pass. The goal is to establish canonical domain types for market identity and exchange selection, create stable Application-layer contracts for exchange capabilities, adapt Hyperliquid behind those contracts first, and then plug Binance into the same seams in phases.

This plan is intentionally split into a low-risk first implementation and two follow-on plans. The first implementation stops after the Hyperliquid-first abstraction seam is in place. Persistence-wide canonical symbol migration and actual Binance runtime enablement are deferred so they can be estimated, tested, and reviewed independently.

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

## Delivery Split

### Implementation Plan A: Exchange Seam Introduction

This is the scope of the current implementation plan.

- Phase 0: Domain foundations that do not require persistence migration
- Phase 1: Neutral Application-layer contracts
- Phase 2: Hyperliquid adapters and cleanup
- Phase 3: Migrate the first exchange-neutral Application consumers

Target outcome:

- Hyperliquid keeps working as the only live venue
- Application code depends on exchange capabilities where that adds value now
- No database-wide symbol rewrite is attempted in the same delivery

### Follow-on Plan B: Canonical Market Persistence Migration

This becomes a separate implementation plan after Plan A is complete.

- Current Phase 3b moves here unchanged in intent
- Includes entity format migration, EF/repository updates, and existing data migration
- Requires dedicated regression testing because it changes storage format and read/write semantics across the system

### Follow-on Plan C: Runtime Selection And Binance Enablement

This becomes a separate implementation plan after Plan A, and after Plan B if canonical storage is required first.

- Current Phases 4 and 5 move here
- Includes keyed DI, exchange resolution, and deciding whether Binance is read-only or live-execution capable
- Should not start until the first seam-based abstractions are proven stable under Hyperliquid

## Recommended PR Sequence For Plan A

Plan A is still too large for a single safe PR. Break it into the following implementation sequence.

### PR 1: Domain And Contract Foundations

Scope:

- Phase 0 in full
- Phase 1 in full

Include:

- `TradingPair` value object
- `Exchange` enum
- `AssetType` relocation to Domain
- Removal of redundant `IBinanceCandleIngestionService`
- New exchange-neutral interfaces in `TradePilot.Application`
- Unit tests for `TradingPair`

Exclude:

- Any refactor of existing Hyperliquid runtime behavior
- Any mapper call-site migration
- Any DI rewiring beyond what is required to compile
- Any persistence/entity storage migration

Why this is the first PR:

- It creates the vocabulary and contracts without touching hot execution paths
- It is the easiest point to review naming, boundaries, and API shape before adapters are built
- Failures here are compile-time and test-time, not runtime behavioral regressions

Acceptance focus:

- Solution compiles cleanly
- Existing tests stay green
- New abstractions are stable enough that later PRs can target them without renaming churn

### PR 2: Hyperliquid Adapters And Cleanup

Scope:

- Phase 2 in full

Include:

- `StateRecoveryService` cleanup to stop using raw `PostInfoAsync` for open orders
- Hyperliquid market metadata adapter
- Hyperliquid account adapter
- Hyperliquid capabilities implementation
- Refactor `HyperliquidAssetMapper` to injectable service
- Refactor `BinanceAssetMapper` to injectable service, but only to establish the seam

Exclude:

- `LiveMarketContextBuilder` consumer migration
- Broad controller rewiring
- Persistence/entity migration
- Runtime exchange selection

Why this is the second PR:

- It keeps runtime changes localized to adapter seams and one cleanup target
- It proves the new contracts can wrap Hyperliquid without forcing broad consumer changes yet
- It isolates mapper refactor fallout before touching higher-level orchestration

Acceptance focus:

- Hyperliquid live behavior is unchanged
- New adapters are covered with targeted tests where practical
- No raw protocol-level open-order query remains in `StateRecoveryService`

### PR 3: First Consumer Migration

Scope:

- Phase 3.1 and Phase 3.2 are required
- Phase 3.3 is optional and should only include clearly exchange-neutral read paths
- Phase 3.4 remains the guardrail

Include:

- `LiveMarketContextBuilder` migration to `IExchangeMarketMetadataProvider`
- `StateRecoveryService` migration to `IExchangeAccountClient`
- Minimal supporting DI changes

Exclude:

- Opportunistic migration of every Hyperliquid dependency in the API and Worker
- WebSocket abstraction work
- Entity storage migration
- Binance registration and keyed DI

Why this is the third PR:

- It is the first point where the Application layer meaningfully consumes the new abstractions
- The blast radius is still bounded to a small number of consumers
- It gives a clean checkpoint to evaluate whether the abstractions are actually buying anything before continuing

Acceptance focus:

- Both services behave the same under Hyperliquid
- DI remains explicit and understandable
- No broad follow-on churn is required to land the PR

### Explicitly Not In Plan A

The following work must stay out of the first implementation wave even if it appears mechanically close:

- Canonical persistence migration for `Symbol`, `Market`, `Source`, and `Exchange`
- SQLite data migration scripts for existing rows
- Broad repository and EF rewrites to canonical storage
- Keyed DI runtime exchange resolution
- Binance runtime support beyond seam-level mapper and adapter preparation

### Recommended Landing Order

1. PR 1: Domain and contract foundations
2. PR 2: Hyperliquid adapters and cleanup
3. PR 3: First consumer migration
4. Reassess before creating Plan B and Plan C branches

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
    public string Quote { get; init; }         // "USD" (canonical), "USDT" (Binance-specific)
    public AssetType ProductType { get; init; } // Perp, Spot

    // Canonical string format: "BTC/USD:PERP", "ETH/USD:SPOT"
    public string Canonical => $"{Base}/{Quote}:{ProductType}";

    public static TradingPair Parse(string canonical) { /* parse from canonical string */ }
}
```

#### Canonical Quote Currency: `USD`

**Decision**: The application-internal quote currency is always **`USD`**. Exchange-specific quote conventions (`USDT` for Binance, implicit USD for Hyperliquid) are handled by `IExchangeSymbolMapper` at the boundary — never stored in domain entities.

- `BTC/USD:PERP` is the canonical form, regardless of whether the exchange quotes in USD, USDT, or USDC
- The symbol mapper normalizes: Binance `BTCUSDT` → `BTC/USD:PERP`, Hyperliquid `BTC` → `BTC/USD:PERP`
- If an exchange uses USDT as the actual settlement currency, that is an exchange-level detail, not a domain-level one
- This avoids proliferating `BTC/USD:PERP` vs `BTC/USDT:PERP` as separate markets for the same logical position

This resolves:
- **Ambiguity**: `"BTC"` alone doesn't tell you spot vs perp — `TradingPair` makes it explicit
- **Mapping direction**: Each exchange mapper converts to/from `TradingPair`, not from free strings
- **Spot support**: Hyperliquid is perps-only, Binance supports spot. The `ProductType` field makes this a first-class concern rather than an implicit assumption
- **Quote normalization**: No duplication of logical markets due to exchange-specific quote currency naming

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
| `BTC/USD:PERP` | `BTC` (bare coin) | `BTCUSDT` | Both map to same canonical; quote normalized to USD |
| `ETH/USD:PERP` | `ETH` | `ETHUSDT` | |
| `SOL/USD:PERP` | `SOL` | `SOLUSDT` | |
| `BTC/USD:SPOT` | N/A (not supported) | `BTCUSDT` | Spot not available on Hyperliquid; `IExchangeCapabilities` rejects |

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
- [ ] Task 0.5: Unit tests for `TradingPair` parsing, canonical format round-trip, equality, and quote normalization

Acceptance criteria:

- `TradingPair` compiles and is usable from Domain and Application layers
- `TradingPair` always uses `USD` as the canonical quote currency
- `AssetType` is a Domain enum, all existing references update cleanly
- `Exchange` enum exists but does **not** yet replace free strings in entities (entity migration is Phase 3b)
- Existing tests pass with no behavior change

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

### [x] Phase 2: Hyperliquid Adapters And Pre-Migration Cleanup

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Refactor `StateRecoveryService` to use `IHyperliquidAccountService.GetOpenOrdersAsync()` instead of raw `PostInfoAsync<JsonElement>(new { type = "openOrders", user = ... })` — this is a Hyperliquid-internal cleanup that makes Phase 3 migration straightforward
- [x] Task 2.2: Create a Hyperliquid market-metadata adapter implementing `IExchangeMarketMetadataProvider` over `IHyperliquidRestClient.GetMarketInfoAsync`
- [x] Task 2.3: Create a Hyperliquid account adapter implementing `IExchangeAccountClient` over `IHyperliquidAccountService`
- [x] Task 2.4: Refactor `HyperliquidAssetMapper` from a static class to an injectable `IExchangeSymbolMapper` implementation using `TradingPair`
- [x] Task 2.5: Add a Hyperliquid capabilities implementation capturing current venue support (`SupportedProductTypes = {Perp}`, leverage = true, etc.)
- [x] Task 2.6: Similarly refactor `BinanceAssetMapper` from a static class to an injectable `IExchangeSymbolMapper` implementation

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

### [ ] Deferred To Follow-on Plan B: Migrate Entity Fields To Canonical Types

**Complexity**: Medium | **Risk**: Medium

This work is intentionally split out of the current implementation plan. It migrates domain entity fields from free strings to the canonical `TradingPair` and `Exchange` types introduced in Phase 0. It is separated from the consumer migration (Phase 3) because it requires database schema changes, data migration, and broad regression coverage.

**Entities with `Symbol` (free string → `TradingPair.Canonical` string format):**

| Entity | Current field | Current format | Canonical format |
|---|---|---|---|
| `Candle` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `GridCycle` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `LiveOrder` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `LiveFill` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `BacktestRun` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `FundingRate` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `LlmContextSnapshot` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `OptimizationRun` | `Symbol` | `"BTC-PERP"` | `"BTC/USD:PERP"` |
| `StrategyTemplate` | `Market` | `"BTC-PERP"` | `"BTC/USD:PERP"` |

**Entities with `Exchange`/`Source` (free string → `Exchange` enum stored as string):**

| Entity | Current field | Current value | New value |
|---|---|---|---|
| `Candle` | `Source` | `"Hyperliquid"` | `Exchange.Hyperliquid` (stored as `"Hyperliquid"`) |
| `UserWalletAddress` | `Exchange` | `"Hyperliquid"` | `Exchange.Hyperliquid` (stored as `"Hyperliquid"`) |

- [ ] Task 3b.1: Add a `TradingPairConverter` utility that maps legacy symbol formats (`"BTC-PERP"`, `"BTC"`) to canonical `TradingPair` format (`"BTC/USD:PERP"`)
- [ ] Task 3b.2: Write a SQLite data migration script to update existing `Symbol` / `Market` values in all affected tables
- [ ] Task 3b.3: Update entity `Create()` factories and setters to validate/normalize to canonical format on write
- [ ] Task 3b.4: Update entity `Source` / `Exchange` fields to use `Exchange` enum (stored as string for SQLite compatibility)
- [ ] Task 3b.5: Update all repository queries and EF configurations for the new formats
- [ ] Task 3b.6: Update `OrderRequest.Symbol` to use canonical format
- [ ] Task 3b.7: Update `TierFeaturePolicy.AllowedAssets` to use canonical base asset names
- [ ] Task 3b.8: Comprehensive test pass — verify all existing integration and domain tests pass with canonical formats

Acceptance criteria:

- All entity `Symbol` fields store canonical `TradingPair` format (`BTC/USD:PERP`)
- All entity `Source`/`Exchange` fields use the `Exchange` enum
- Existing data is migrated via script — no data loss
- All queries, repositories, and services work with the new format
- Legacy format strings (`"BTC-PERP"`, `"BTC"`) are no longer written by any code path

### [ ] Deferred To Follow-on Plan C: Runtime Selection And DI Composition

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

### [ ] Deferred To Follow-on Plan C: Add Binance Through The New Seams

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

### [x] Phase 2 initial file set

- [x] `src/TradePilot.Application/Trading/Services/StateRecoveryService.cs` (cleanup: replace raw `PostInfoAsync` with `IHyperliquidAccountService`)
- [x] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidMarketMetadataProvider.cs` (new adapter)
- [x] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAccountAdapter.cs` (new adapter)
- [x] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` (refactor: static → injectable `IExchangeSymbolMapper`)
- [x] `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs` (new)
- [x] `src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs` (refactor: static → injectable `IExchangeSymbolMapper`)

### [ ] Follow-on Plan B initial file set

- [ ] `src/TradePilot.Domain/ValueObjects/TradingPairConverter.cs` (new — legacy format → canonical converter)
- [ ] `src/TradePilot.Domain/Entities/Candle.cs` (update `Symbol` format, `Source` to `Exchange` enum)
- [ ] `src/TradePilot.Domain/Entities/GridCycle.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/LiveOrder.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/LiveFill.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/BacktestRun.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/FundingRate.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/LlmContextSnapshot.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/OptimizationRun.cs` (update `Symbol` format)
- [ ] `src/TradePilot.Domain/Entities/StrategyTemplate.cs` (update `Market` format)
- [ ] `src/TradePilot.Domain/Entities/UserWalletAddress.cs` (update `Exchange` to enum)
- [ ] `src/TradePilot.Application/Trading/Models/OrderRequest.cs` (update `Symbol` format)
- [ ] SQLite migration script (new — data migration for existing rows)
- [ ] `tests/TradePilot.Domain.Tests/ValueObjects/TradingPairConverterTests.cs` (new)

## Risks And Design Concerns

### Scope Bundling Risk

- Combining abstraction seams, canonical persistence migration, and Binance runtime enablement in one implementation would create a large blast radius across Application, Domain, Infrastructure, Persistence, and seed data
- The split above keeps the first delivery focused on dependency direction and adapter seams rather than storage rewrites
- Plan B and Plan C should be reviewed as separate deliveries with their own verification strategy and rollback thinking

### Symbol Mapping Risk

- Hyperliquid uses bare coin names (`BTC`), Binance uses pair symbols (`BTCUSDT`)
- Both current mappers are static classes with hardcoded dictionaries — refactoring to injectable services requires updating all call sites
- The canonical `TradingPair` format (`BTC/USD:PERP`) with `USD` as the standard quote normalizes both exchanges to a single market identity

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

- The current implementation finishes at the abstraction seam introduced by Phases 0-3
- Application services stop depending directly on Hyperliquid contracts where exchange-neutral behavior is sufficient
- Hyperliquid remains the current live trading venue with no behavior regression
- Exchange capabilities are explicit and testable
- Binance can be introduced behind the same market-data and account seams in a follow-on plan
- The worker key-custody model remains intact
- Hyperliquid DCA on perps remains unchanged

## Resolved Decisions

- **Canonical quote currency**: `USD`. Exchange-specific quotes (USDT, USDC) are normalized at the `IExchangeSymbolMapper` boundary. Domain entities always store `USD`.
- **Entity migration timing**: Deferred to a dedicated follow-on plan after the initial abstraction seam is implemented and verified.
- **Delivery shape**: The work is split into Plan A (abstractions), Plan B (canonical persistence migration), and Plan C (runtime selection plus Binance enablement).

## Open Questions

- Should Binance initial support be historical/read-only or live-execution capable?
- Should `IExecutionEngine` be renamed, or is keeping it stable the better migration path?
- Is a thin `IExchangeClient` aggregator useful enough to justify the extra layer now, or should keyed DI handle resolution without it?

## Recommendation

Proceed with capability interfaces first and keep `IExecutionEngine` as the current execution seam for the first migration wave. Do not introduce a large god-interface `IExchangeClient`. If a top-level exchange root is added, keep it as a thin aggregator over smaller capabilities.

Stop the current implementation after Phase 3. Treat the old Phase 3b and Phases 4-5 as separate follow-on plans so storage migration and Binance runtime enablement are not coupled to the first abstraction pass.

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-19T18:58:29Z | 2026-04-19T18:58:29Z |
| Plan Implementer | in-progress | 2026-04-19T20:18:04Z | - |