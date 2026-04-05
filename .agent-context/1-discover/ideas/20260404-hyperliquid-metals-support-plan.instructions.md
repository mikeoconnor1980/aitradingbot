---
applyTo: ".agent-context/3-develop/build/changes/20260404-hyperliquid-metals-support-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-04T16:26:03Z"
status: "planned"
lastUpdated: "2026-04-04T16:26:03Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Hyperliquid Metals Support

## Overview

Extend the platform so Gold and Silver instruments listed on Hyperliquid can be supported consistently across manual trading, live market data, strategy authoring, historical candle ingestion, and backtesting, without relying on BTC-specific defaults or Binance-only validation paths.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Risk Level:** High
**Depends On:** Hyperliquid listing a tradable gold or silver market; historical data source decision for backtesting

### User Story

> As a trader, I want the app to support Gold and Silver markets on Hyperliquid so that I can view market data, place manual orders, configure strategies, and run backtests for those instruments using the same workflows currently available for supported crypto markets.

### Acceptance Criteria

- [ ] **Given** Hyperliquid lists a supported gold or silver instrument and the app catalog includes it, **When** the user opens manual trading screens, **Then** the instrument appears in the asset selector with the correct symbol, leverage limits, and size precision
- [ ] **Given** a supported metal instrument is selected, **When** the user opens market data screens, **Then** REST market info, candle history, and live price updates are shown for that instrument instead of only BTC
- [ ] **Given** a supported metal instrument exists, **When** the user opens the strategy builder, **Then** it is available in the market list without requiring hard-coded symbol edits in the frontend
- [ ] **Given** a supported metal instrument is requested for Hyperliquid candle ingestion, **When** the API validates the request, **Then** the symbol is accepted and the candles are persisted successfully
- [ ] **Given** historical candles exist for the chosen metal instrument, **When** the user validates or runs a backtest, **Then** the backtest pipeline accepts the instrument and loads the correct candle source instead of failing on Binance-only symbol rules
- [ ] **Given** a manual order is placed for a supported metal instrument, **When** Hyperliquid accepts the order, **Then** fills, positions, and open orders are returned and enriched correctly for that instrument
- [ ] **Given** automated strategy trading for metals is enabled in scope, **When** a confirmed candle closes, **Then** the runtime can evaluate the strategy and route approved signals through a live execution path for that instrument
- [ ] **Given** an unsupported or not-yet-listed metal symbol is requested, **When** the user or API submits it, **Then** the system returns a clear validation error rather than exposing a broken or partial workflow

## Objectives

- Replace split asset assumptions with a single canonical instrument-support model
- Remove BTC-only live market data behavior from the SignalR and Angular flows
- Make strategy market selection and candle ingestion validation derive from the same supported-instrument source
- Make backtesting source-aware so metals are not blocked by Binance-only symbol validation
- Document and gate the live automation work separately from manual trading if the execution runtime remains incomplete

### Discovery References

- Hyperliquid public `meta` query currently does not show obvious `XAU`, `XAG`, `GOLD`, `SILVER`, `PAXG`, or `XAUT` markets; exchange listing is an external gate
- Manual order asset discovery is already dynamic via `HyperliquidAssetMetadataCache` and `OrdersController`
- Strategy market reference data, Hyperliquid candle-ingestion validation, and Binance-backed backtests still rely on hard-coded symbol mappers
- `MarketDataStreamService` is still a single-asset BTC relay and must be generalized for live multi-asset support
- The worker host is still only wiring persistence startup; automated live strategy execution requires additional runtime composition beyond asset onboarding

### Project Patterns

- `src/TradingApp.Api/Controllers/OrdersController.cs` - dynamic asset list pattern using `IHyperliquidAssetMetadataCache`
- `src/TradingApp.Api/Services/HyperliquidAssetMetadataCache.cs` - exchange metadata cache for asset index, size decimals, and leverage
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` - current static Hyperliquid display-name and timeframe mapping
- `src/TradingApp.Api/Controllers/ReferenceDataController.cs` - strategy-builder market list endpoint
- `src/TradingApp.Api/Controllers/CandlesController.cs` - Hyperliquid candle-ingestion validation path
- `src/TradingApp.Api/Services/MarketDataStreamService.cs` - SignalR live price aggregation and broadcast service
- `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` - WebSocket subscription surface and trade message parsing
- `src/TradingApp.Api/Controllers/BacktestsController.cs` - current Binance-normalized backtest validation and execution entry point
- `src/TradingApp.Infrastructure/Binance/BinanceAssetMapper.cs` - current Binance-only backtest symbol allowlist
- `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` - already supports optional `source`, which can be used to make backtesting provider-aware
- `src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs` - historical candle loading path that will need source-aware wiring
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` - manual order UI with BTC-seeded defaults
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` - market-data UI with BTC-seeded defaults
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` - strategy builder default market selection

### [ ] Phase 0: Exchange and Product Gating

**Complexity**: Low | **Risk**: High

- [ ] Task 0.1: Confirm the exact Hyperliquid-listed symbols to support for Gold and Silver
- [ ] Task 0.2: Decide the canonical internal naming convention for metal markets across `-PERP`, `-USD`, and raw coin/exchange symbols
- [ ] Task 0.3: Decide whether scope is manual trading only or includes strategy automation and backtesting
- [ ] Task 0.4: Decide the historical data provider for metal backtests if Binance does not offer matching symbols

### [ ] Phase 1: Canonical Instrument Catalog

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 1.1: Introduce a single instrument catalog abstraction or service for supported tradable markets
- [ ] Task 1.2: Refactor Hyperliquid symbol validation to derive from the catalog or metadata cache instead of a static allowlist
- [ ] Task 1.3: Preserve timeframe mapping while separating it from supported-asset membership logic
- [ ] Task 1.4: Update `ReferenceDataController` and related DTOs to return catalog-backed strategy markets
- [ ] Task 1.5: Add tests covering supported-market discovery, display-name normalization, and unsupported-symbol handling

### [ ] Phase 2: Manual Trading and Live Market Data

**Complexity**: High | **Risk**: Medium

- [ ] Task 2.1: Generalize `MarketDataStreamService` to subscribe to more than one coin and broadcast asset-specific price updates
- [ ] Task 2.2: Extend `IHyperliquidWebSocketClient` and `HyperliquidWebSocketClient` if needed for multi-asset subscription management
- [ ] Task 2.3: Verify manual order placement, fills, positions, trigger orders, and leverage updates continue to work for catalog-backed instruments
- [ ] Task 2.4: Remove BTC-seeded defaults from Angular market-data and order-entry components and drive initial asset selection from API data
- [ ] Task 2.5: Add backend and frontend tests covering asset switching, price updates, and symbol-specific UI behavior

### [ ] Phase 3: Strategy Markets and Candle Ingestion

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 3.1: Make strategy-builder market lists fully dynamic from supported reference data
- [ ] Task 3.2: Update Hyperliquid candle-ingestion validation to accept supported metal instruments
- [ ] Task 3.3: Ensure ingested candle symbols are normalized consistently with the instrument catalog
- [ ] Task 3.4: Add ingestion and controller tests for accepted and rejected metal-symbol scenarios
- [ ] Task 3.5: Update strategy-builder and backtest-form frontend tests that currently assume `BTC-USD`

### [ ] Phase 4: Backtesting and Historical Data Source Support

**Complexity**: High | **Risk**: High

- [ ] Task 4.1: Decide whether metal backtests use Binance, Hyperliquid, or another provider for historical candles
- [ ] Task 4.2: Remove Binance-only symbol validation from `BacktestsController` and related request validation paths
- [ ] Task 4.3: Pass candle `source` explicitly through coverage checks and replay loading so backtests use the intended provider
- [ ] Task 4.4: Update backtest API and UI models if provider selection must be user-visible or strategy-configurable
- [ ] Task 4.5: Add repository, query, and end-to-end backtest tests covering provider-aware candle lookup for metals

### [ ] Phase 5: Live Strategy Automation Runtime

**Complexity**: High | **Risk**: High

- [ ] Task 5.1: Confirm whether automated live strategy trading is in scope for this workstream
- [ ] Task 5.2: Build a live `IExecutionEngine` implementation using Hyperliquid order services if automation is required
- [ ] Task 5.3: Introduce a live `IPositionManager` and runtime composition that is not backtest-specific
- [ ] Task 5.4: Expand `TradingApp.Worker` from persistence startup only into a candle-close runtime host using `CandleClock` and `StrategyScheduler`
- [ ] Task 5.5: Add reconnect recovery and per-user execution checkpoints for live multi-subscriber automation

### [ ] Phase 6: Risk Tuning, Verification, and Rollout

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 6.1: Define market-specific leverage, size, spacing, and stop-loss defaults for metals rather than inheriting crypto assumptions
- [ ] Task 6.2: Add validation and operator-facing error messages for unsupported or partially configured metal markets
- [ ] Task 6.3: Run end-to-end verification for manual order placement, positions, candles, and backtests on the selected symbols
- [ ] Task 6.4: Document rollout sequencing, operational checks, and fallback behavior if the exchange delists or renames a metal instrument

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 0: Exchange and Product Gating | Low | High |
| Phase 1: Canonical Instrument Catalog | Medium | Medium |
| Phase 2: Manual Trading and Live Market Data | High | Medium |
| Phase 3: Strategy Markets and Candle Ingestion | Medium | Medium |
| Phase 4: Backtesting and Historical Data Source Support | High | High |
| Phase 5: Live Strategy Automation Runtime | High | High |
| Phase 6: Risk Tuning, Verification, and Rollout | Medium | Medium |
| **Total** | **High** | **High** |

### Scoping Notes

- Hyperliquid exchange listing is a hard precondition; without a tradable metal market, implementation should stop after discovery and validation messaging
- Manual trading support is materially smaller than full bot/backtest support; if scope is reduced, Phase 5 can be deferred
- The repository already stores candle `Source`, and `ICandleRepository` already supports an optional `source` parameter, so provider-aware backtests are feasible without redesigning the candle entity
- `MarketDataStreamService` currently emits only BTC price updates, so simply adding metals to dropdowns would create a broken partial experience
- The worker host does not yet run live strategy scheduling or execution loops, so “trade Gold and Silver automatically” is not just an asset-onboarding task

## Dependencies

- Hyperliquid lists one or more tradable metal markets suitable for Gold and Silver exposure
- Product decision on whether scope is manual trading only or includes strategy automation and backtesting
- Historical candle provider decision for metal backtests
- Approval of risk limits and sizing defaults specific to the chosen metal instruments

## Success Criteria

- A supported Hyperliquid metal instrument can be selected in the UI for manual trading and market-data views without BTC-specific workarounds
- Supported metal instruments appear consistently in order entry, reference data, candle ingestion, and backtest validation paths
- Candle ingestion and market-data retrieval succeed for the supported metal instruments using consistent normalization rules
- Backtests either run successfully for supported metals using an approved historical provider or fail with a clear unsupported-provider message
- If live automation is included, the runtime can evaluate and execute approved signals for metal instruments on confirmed candle closes

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-04T16:26:03Z | 2026-04-04T16:26:03Z |