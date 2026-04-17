---
applyTo: ".agent-context/3-develop/build/changes/20260324-f3-market-data-rest-changes.md"
currentAgent: "Implementation Reviewer"
agentStartedAt: "2026-03-24T22:38:38Z"
status: "complete"
lastUpdated: "2026-03-24T23:08:23Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F3 — Market Data (REST)

## Overview

Fetch and display market metadata and recent candle data for perpetual assets via the Hyperliquid REST API, introducing the Application layer with MediatR CQRS, ApiController base class, and production-grade error handling infrastructure.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**PRD:** hyperliquid-poc-prd.md
**Implementation Phase:** 2
**Risk Level:** Low
**Depends On:** F1

### Summary

Fetch and display market metadata and recent candle data for perpetual assets via the Hyperliquid REST API. This is the lowest-risk feature that retrieves real exchange data and validates the response parsing pipeline end-to-end.

### Acceptance Criteria

- [ ] **Given** the Market Data page is loaded, **When** the page renders, **Then** BTC-PERP is selected by default in the asset dropdown
- [ ] **Given** the page has loaded, **When** market data is fetched, **Then** the market info card displays mid price, mark price, index price, funding rate, 24h volume, open interest, and 24h price change % for the selected asset
- [ ] **Given** the page has loaded, **When** candle data is fetched, **Then** the candle table displays 50 recent candles sorted newest first with OHLCV columns
- [ ] **Given** the candle table is displayed, **When** the default timeframe loads, **Then** the 15m timeframe is selected
- [ ] **Given** the candle table is displayed, **When** the user selects a different timeframe (1H or 4H), **Then** the table reloads with candles for the new timeframe
- [ ] **Given** the asset dropdown is displayed, **When** the user selects a different asset, **Then** both the market info card and candle table reload for the new asset
- [ ] **Given** the page is displayed, **When** 10 seconds elapse, **Then** the market info card auto-refreshes with latest data (candle table does not auto-refresh)
- [ ] **Given** the page is displayed, **When** the user clicks the manual refresh button, **Then** both market info and candle data reload immediately
- [ ] **Given** the Hyperliquid API is unreachable, **When** a fetch attempt fails, **Then** the UI displays a meaningful error message and retries on the next poll cycle
- [ ] **Given** no candle data is available for the selected timeframe, **When** the table renders, **Then** an empty state message is shown

## Objectives

- Introduce the Application project with MediatR CQRS query infrastructure
- Create ApiController base class, Envelope response type, and HttpGlobalExceptionFilter
- Extend HyperliquidRestClient with market info and candle data methods
- Implement MarketDataController with two GET endpoints dispatching MediatR queries
- Build Angular market data page with asset selector, market info card, timeframe selector, candle table, and manual refresh
- Establish BaseControllerTests and test the full backend pipeline

### Discovery References

- **F1 provides**: Solution scaffolding (TradePilot.sln), Api project, Infrastructure project, HyperliquidRestClient (connectivity check), HyperliquidOptions, Program.cs with DI/CORS/config
- **F3 introduces**: Application project, MediatR, CQRS queries/handlers, ApiController base, Envelope, HttpGlobalExceptionFilter, BaseControllerTests, AutoMapper
- **Hyperliquid API**: POST `/info` with `{"type": "metaAndAssetCtxs"}` for market metadata; POST `/info` with `{"type": "candleSnapshot", "req": {...}}` for candle data — no auth required
- **Angular**: Standalone components, Angular Material, ApiRestClient wrapper, interval-based polling with takeUntilDestroyed
- **Greenfield gap**: No Application project, MediatR, base classes, or test infrastructure exist yet — F3 creates all of it

### Project Patterns

- `.github/instructions/api-controllers.instructions.md` - Controller base class, MediatR dispatch, Envelope errors, ProducesResponseType
- `.github/instructions/dotnet-architecture.instructions.md` - Bounded context structure, CQRS queries, infrastructure services, AutoMapper
- `.github/instructions/csharp.instructions.md` - Sealed classes, IOptions, async/await, Guard classes, Given_When_Then tests
- `.github/instructions/testing.instructions.md` - MSTest, Moq, FluentAssertions ≤v6, BaseControllerTests, builder pattern
- `.github/instructions/angular.instructions.md` - Service patterns, observable lifecycle, component structure
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` - Hyperliquid REST API (POST /info), shared market data (no auth)
- `.agent-context/0-knowledge/06-project-structure.md` - Multi-project layered architecture (Api, Application, Infrastructure, Domain)
- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` - F1 foundation: HyperliquidRestClient, HyperliquidOptions, Program.cs

### [x] Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API

**Complexity**: High | **Risk**: Medium

- [x] Task 1.1: Create Application project and add MediatR infrastructure
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-11-create-application-project-and-add-mediatr-infrastructure

- [x] Task 1.2: Create ApiController base class, Envelope, and HttpGlobalExceptionFilter
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-12-create-apicontroller-base-class-envelope-and-httpglobalexceptionfilter

- [x] Task 1.3: Create MarketData DTOs and Hyperliquid response models
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-13-create-marketdata-dtos-and-hyperliquid-response-models

- [x] Task 1.4: Extend HyperliquidRestClient with market info and candle methods
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-14-extend-hyperliquidrestclient-with-market-info-and-candle-methods

- [x] Task 1.5: Create MediatR queries and handlers for market data
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-15-create-mediatr-queries-and-handlers-for-market-data

- [x] Task 1.6: Create MarketDataController with GET endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-16-create-marketdatacontroller-with-get-endpoints

- [x] Task 1.7: Update Program.cs with MediatR, AutoMapper, and exception filter registration
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-17-update-programcs-with-mediatr-automapper-and-exception-filter-registration

- [x] Task 1.8: Create test infrastructure and write backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-18-create-test-infrastructure-and-write-backend-tests

- [x] Task 1.9: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-01-details.md#task-19-build-solution-and-run-all-tests

### [x] Phase 2: Frontend — Angular Market Data Page

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Install Angular Material and create ApiRestClient wrapper
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-21-install-angular-material-and-create-apirestclient-wrapper

- [x] Task 2.2: Create market data models and DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-22-create-market-data-models-and-dtos

- [x] Task 2.3: Create market data API service
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-23-create-market-data-api-service

- [x] Task 2.4: Create market data page component with asset selector, market info card, timeframe selector, candle table, and refresh button
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-24-create-market-data-page-component

- [x] Task 2.5: Configure routing and navigation
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-25-configure-routing-and-navigation

- [x] Task 2.6: Implement 10-second market info polling and manual refresh
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-26-implement-polling-and-manual-refresh

- [x] Task 2.7: Implement error states and empty states
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-27-implement-error-states-and-empty-states

- [x] Task 2.8: Build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260324-f3-market-data-rest-phase-02-details.md#task-28-build-and-lint-verification

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Application Layer, MediatR Infrastructure, Market Data API | High | Medium |
| Phase 2: Frontend — Angular Market Data Page | Medium | Low |
| **Total** | **Medium-High** | **Low-Medium** |

### Scoping Notes

- F3 introduces the Application project, MediatR, AutoMapper, ApiController base, Envelope, HttpGlobalExceptionFilter, and BaseControllerTests — significant one-time infrastructure cost
- Hyperliquid REST API uses POST /info with type fields, not GET — HyperliquidRestClient translates between the GET controller endpoints and the POST exchange API
- Asset list is hardcoded (BTC-PERP, ETH-PERP, SOL-PERP, etc.) — no dynamic fetching from exchange
- Candle data limited to 50 candles per request
- Angular Material is the UI framework (not DDX/DDS from instruction files)
- No database persistence — all data fetched on-demand from Hyperliquid
- Exact Hyperliquid JSON response schemas should be verified against exchange documentation during implementation

## Dependencies

- **MediatR** NuGet package — CQRS dispatch
- **AutoMapper** NuGet package — DTO mapping
- **FluentAssertions ≤v6** NuGet package — Test assertions
- **Moq** NuGet package — Test mocking
- **Angular Material** npm package — UI components
- **F1 implementation** — Solution, Api project, Infrastructure project, HyperliquidRestClient, HyperliquidOptions, Program.cs

## Success Criteria

- `dotnet build` succeeds for the entire solution including new Application and test projects
- `dotnet test` passes all backend tests (controller tests via BaseControllerTests, HyperliquidRestClient mock tests)
- `GET /api/market/info?asset=BTC-PERP` returns MarketInfoDto with all required fields
- `GET /api/market/candles?asset=BTC-PERP&timeframe=15m` returns 50 CandleDtos sorted newest first
- Invalid asset returns 404; invalid timeframe returns 400
- Angular app builds and lints cleanly (`ng build`, `ng lint`)
- Market data page displays asset selector (BTC-PERP default), market info card, timeframe selector (15m default), candle table
- Market info card auto-refreshes every 10 seconds
- Switching asset or timeframe reloads data correctly
- Manual refresh button works for both market info and candles
- Error states display meaningful messages; empty states show appropriate messages

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-24T19:23:59Z | 2026-03-24T19:47:50Z |
| Plan Reviewer | plan-reviewed | 2026-03-24T19:48:38Z | 2026-03-24T19:59:32Z |
| Plan Implementer | in-progress | 2026-03-24T20:05:00Z | - |
| Plan Implementer | implemented | 2026-03-24T20:05:00Z | 2026-03-24T20:30:00Z |
| Implementation Reviewer | complete | 2026-03-24T22:38:38Z | 2026-03-24T23:08:23Z |
