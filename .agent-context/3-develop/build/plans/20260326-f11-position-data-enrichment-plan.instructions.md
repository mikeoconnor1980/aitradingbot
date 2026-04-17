---
applyTo: ".agent-context/3-develop/build/changes/20260326-f11-position-data-enrichment-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-26T19:48:07Z"
status: "implemented"
lastUpdated: "2026-03-26T19:54:39Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F11 — Position Data Enrichment

## Overview

Enrich the positions table and position detail view with mark price display, notional value, margin used, and funding rate fields sourced from the Hyperliquid API.

## PBI Details

**PBI ID:** Draft  
**Feature:** F11 — Position Data Enrichment  
**Implementation Phase:** 7  
**Risk Level:** Low  
**Depends On:** F2, F4

### User Story

> As a **trader**, I want to **see the mark price, notional value, margin used, and funding rate for each position** so that **I can understand my true exposure, margin consumption, and position economics without switching to the Hyperliquid UI**.

### Acceptance Criteria

- [x] **Given** I have open positions, **When** the positions table loads, **Then** I see Mark Price, Notional, Margin, and Funding columns for each position
- [x] **Given** a Long BTC position with entry 71,464 and mark 72,000, **When** the table renders, **Then** Mark Price shows 72,000 with a green indicator (price moving in favour)
- [x] **Given** a Short BTC position with entry 71,464 and mark 72,000, **When** the table renders, **Then** Mark Price shows 72,000 with a red indicator (price moving against)
- [x] **Given** a position with size 0.0276 and mark price 71,200, **When** the Notional column renders, **Then** it displays "$1,969.12"
- [x] **Given** a 5× leveraged position with notional $1,969.12, **When** the Margin column renders, **Then** it shows approximately "$393.82"
- [x] **Given** I hover over the Margin value, **When** the tooltip appears, **Then** it shows the margin as a percentage of total equity
- [x] **Given** the current funding rate is negative and I hold a Short position, **When** the Funding column renders, **Then** it shows the rate in green (receiving funding)
- [x] **Given** I hover over the Funding indicator, **When** the tooltip appears, **Then** it shows the hourly rate and estimated daily USD cost/income
- [x] **Given** mark price data is unavailable from the API, **When** the table renders, **Then** Mark Price shows "—" and Notional shows "—"
- [x] **Given** data refreshes every 2 seconds, **When** mark price changes, **Then** the Mark Price, Notional, and Margin columns update accordingly

## Objectives

- Add `MarginUsed` and `FundingRate` fields to `PositionDto` and the position mapping pipeline
- Fetch funding rates from the Hyperliquid `metaAndAssetCtxs` endpoint and join with position data by asset name
- Display Mark Price column in the positions table (already in DTO but not rendered)
- Display Notional, Margin, and Funding columns with formatting, color-coding, and tooltips
- Create a reusable `FundingIndicatorComponent` for color-coded funding rate display
- Add unit tests for the enriched service mapping and integration tests for the enriched endpoint

### Discovery References

**Key finding: `MarkPrice` is already in `PositionDto` and already mapped from `clearinghouseState.markPx`.** The PBI lists it as new, but only the table column rendering is needed.

**Key finding: `marginUsed` and `positionValue` are present in the Hyperliquid `clearinghouseState` response but NOT yet extracted** in `HyperliquidAccountService.MapToPositions()`. Adding them requires only parsing two additional JSON fields.

**Key finding: Funding rate requires a separate `metaAndAssetCtxs` API call.** The `HyperliquidAssetCtx` model already has `Funding` and `MarkPx` string fields. The `HyperliquidAssetMetadataCache` provides coin→index mapping for cross-referencing the two API responses.

**Key finding: `GetMarketInfoAsync` in `HyperliquidRestClient` already calls `metaAndAssetCtxs` and parses `HyperliquidAssetCtx.Funding`.** The pattern for fetching and parsing funding rates is fully established.

### Project Patterns

- `src/TradePilot.Api/Models/PositionDto.cs` — DTO to extend with MarginUsed, FundingRate
- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — Service mapping to extend (JsonElement parsing pattern)
- `src/TradePilot.Api/Services/IHyperliquidAccountService.cs` — Service interface (no change needed)
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — Pattern for `metaAndAssetCtxs` call (GetMarketInfoAsync)
- `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidAssetCtx.cs` — Already has Funding, MarkPx fields
- `src/TradePilot.Api/Services/HyperliquidAssetMetadataCache.cs` — Coin→index mapping for metaAndAssetCtxs cross-reference
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — Presentational table component to extend
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — Template to add columns
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Parent with polling and equity access
- `frontend/trading-ui/src/app/core/models/position.model.ts` — TypeScript interface to extend
- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Integration test pattern to extend
- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Unit test pattern for services

### [ ] Phase 1: Backend — Enrich PositionDto with MarginUsed and FundingRate

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Add MarginUsed and FundingRate properties to PositionDto
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-11-add-marginused-and-fundingrate-to-positiondto

- [x] Task 1.2: Extract marginUsed from clearinghouseState in MapToPositions
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-12-extract-marginused-from-clearinghousestate

- [x] Task 1.3: Add GetFundingRatesAsync method to HyperliquidAccountService
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-13-add-getfundingratesasync-to-account-service

- [x] Task 1.4: Enrich GetPositionsAsync to join funding rates with positions
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-14-enrich-getpositionsasync-with-funding-rates

- [x] Task 1.5: Add HyperliquidAccountService unit tests for enriched mapping
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-15-add-unit-tests-for-enriched-mapping

- [x] Task 1.6: Update AccountControllerTests for enriched PositionDto
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-16-update-accountcontrollertests-for-enriched-dto

- [x] Task 1.7: Run all backend tests and verify
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-01-details.md#task-17-run-all-backend-tests

### [ ] Phase 2: Frontend — Display Enriched Position Data in Positions Table

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Extend Position TypeScript interface with new fields
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-21-extend-position-interface

- [x] Task 2.2: Add Mark Price column to positions table
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-22-add-mark-price-column

- [x] Task 2.3: Add Notional column to positions table
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-23-add-notional-column

- [x] Task 2.4: Add Margin column with equity percentage tooltip
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-24-add-margin-column-with-tooltip

- [x] Task 2.5: Pass equity from DashboardComponent to PositionsTableComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-25-pass-equity-to-positions-table

- [x] Task 2.6: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-02-details.md#task-26-run-frontend-build-and-lint

### [ ] Phase 3: Frontend — Funding Rate Indicator Component

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create FundingIndicatorComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-03-details.md#task-31-create-fundingindicatorcomponent

- [x] Task 3.2: Add Funding column to positions table using FundingIndicatorComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-03-details.md#task-32-add-funding-column-to-positions-table

- [x] Task 3.3: Add responsive column handling for new columns
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-03-details.md#task-33-add-responsive-column-handling

- [x] Task 3.4: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-f11-position-data-enrichment-phase-03-details.md#task-34-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Enrich PositionDto | Medium | Medium |
| Phase 2: Frontend — Display Enriched Data | Medium | Low |
| Phase 3: Frontend — Funding Indicator | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- `MarkPrice` is already in `PositionDto` and mapped — only rendering in the template is new
- `marginUsed` and `positionValue` are already in the Hyperliquid API response — only parsing is needed
- Funding rate requires one additional API call (`metaAndAssetCtxs`) per `GetPositionsAsync` invocation
- Notional value is derived client-side as `abs(size) × markPrice` — no backend field needed
- No database changes required (persistence layer is not yet wired)
- No deployment/DevOps changes needed

## Dependencies

- Hyperliquid API `clearinghouseState` endpoint (existing)
- Hyperliquid API `metaAndAssetCtxs` endpoint (already used by `GetMarketInfoAsync`)
- `@angular/material` `MatTooltipModule` (already installed)
- `HyperliquidAssetMetadataCache` for coin→index mapping (existing singleton)

## Success Criteria

- All 10 acceptance criteria pass when verified against the running application
- All backend tests pass (existing + new unit and integration tests)
- Frontend builds and lints without errors
- Positions table displays Mark Price, Notional, Margin, and Funding columns
- Funding rate is color-coded and has tooltip with hourly rate and daily USD estimate
- Margin has tooltip showing percentage of equity
- Missing data gracefully shows "—" dash

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-26T16:17:45Z | 2026-03-26T17:05:57Z |
| Plan Reviewer | plan-reviewed | 2026-03-26T17:07:47Z | 2026-03-26T17:25:52Z |
| Plan Implementer | implemented | 2026-03-26T19:48:07Z | 2026-03-26T19:54:39Z |
