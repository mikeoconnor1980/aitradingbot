<!-- markdownlint-disable-file -->
# Release Changes: F11 — Position Data Enrichment

**Related Plan**: 20260326-f11-position-data-enrichment-plan.instructions.md
**Implementation Date**: 2026-03-26

## Summary

Completed the F11 positions enrichment work across API and Angular dashboard layers. The backend now keeps mark price sourced from `clearinghouseState`, computes a cross-margin fallback when `marginUsed` is absent, and fetches funding rates independently from `metaAndAssetCtxs`. The positions table now renders explicit Mark Price, Notional, Margin, and Funding columns, with a reusable funding indicator component and narrow-screen column hiding.

## Changes

### Added

- tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs: New unit tests covering enriched position mapping, cross-margin fallback margin calculation, and graceful degradation when funding lookup fails.
- frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.ts: New standalone funding indicator component with funding side semantics and tooltip formatting.
- frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.html: Template rendering funding rate or an unavailable dash.
- frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.scss: Styles for receiving/paying funding states and unavailable display.

### Modified

- src/TradePilot.Api/Services/HyperliquidAccountService.cs: Refactored position enrichment to fetch funding separately, source mark price directly from `clearinghouseState`, and calculate margin fallback from mark price and leverage when the exchange returns zero.
- tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs: Expanded the sample enriched position DTO to include leverage and margin mode alongside funding and margin fields.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts: Registered FundingIndicatorComponent and refactored mark/notional/margin helpers for the new explicit columns.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html: Added Notional and Margin columns, replaced inline funding rendering with the reusable funding indicator, and added graceful dash handling for missing mark price and margin.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss: Added mark-price display helpers, unavailable-state styling, and responsive hiding for the added narrow-screen columns.

### Notes

- `src/TradePilot.Api/Models/PositionDto.cs`, `frontend/trading-ui/src/app/core/models/position.model.ts`, and `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` already contained the required F11 fields/binding in the current repo state, so no further edits were required there.

## Test Results

- Backend tests: PASSED, 78/78 tests passed.
- Frontend build: PASSED.
- Frontend lint: PASSED.

## Issues

- Angular build reported the existing initial bundle budget warning (`597.11 kB` vs `500 kB`). This warning was pre-existing and did not block the build.

## Design Decisions

- Mark price now comes from the position payload itself so the UI still has live mark/notional context even if the funding lookup fails.
- Funding remained a separate lookup from `metaAndAssetCtxs` because that endpoint is the authoritative source for hourly funding.
- Funding display was isolated into a dedicated standalone component so the color logic and tooltip semantics stay out of the table template.

## Release Summary

F11 is implemented and verified. Backend enrichment is covered by new unit tests and existing controller tests, all backend tests pass, and the Angular positions table now exposes the planned Mark Price, Notional, Margin, and Funding fields with responsive handling and reusable funding display logic.