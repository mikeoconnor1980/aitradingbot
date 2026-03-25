---
applyTo: ".agent-context/3-develop/build/changes/20260324-f2-account-dashboard-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-03-24T21:52:07Z"
status: "complete"
lastUpdated: "2026-03-24T22:14:44Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F2 — Account Dashboard

## Overview

Display the Hyperliquid testnet account state (balance, positions, orders) in an Angular Material dashboard with tabbed navigation, 2-second polling, staleness indicators, and error handling.

## PBI Details

**PBI:** [F2-account-dashboard.md](../../backlog/draft/F2-account-dashboard.md)

Display the testnet account state — balance, open positions, and open orders — in an Angular dashboard with tabbed navigation, auto-refresh polling, and visual indicators for data staleness and PnL.

**User Story:** As a developer, I want to see my testnet account state at a glance so that I can monitor my equity, positions, and open orders without switching to the Hyperliquid UI.

**Depends On:** F1 — Configuration & Connectivity (assumed complete)

### Acceptance Criteria

- [ ] **Given** the user has a configured testnet wallet (F1 complete), **When** they navigate to the dashboard, **Then** the account summary displays equity, available margin, cross-margin ratio, maintenance margin, and total unrealised PnL
- [ ] **Given** the user has open positions on testnet, **When** the dashboard loads, **Then** the positions tab shows a table with Asset, Size, Entry Price, Unrealised PnL, and Liquidation Price columns
- [ ] **Given** the user has open orders on testnet, **When** they switch to the orders tab, **Then** a table shows Asset, Side, Price, Size, Order Type, and Status columns
- [ ] **Given** the user has no open positions, **When** the dashboard loads, **Then** the positions tab displays "No open positions"
- [ ] **Given** the user has no open orders, **When** they switch to the orders tab, **Then** it displays "No open orders"
- [ ] **Given** the dashboard is loaded, **When** 2 seconds elapse, **Then** the account data is automatically refreshed from the API
- [ ] **Given** the dashboard is loaded, **When** the user clicks the manual refresh button, **Then** the data is immediately re-fetched and the "Last updated" timestamp resets
- [ ] **Given** the dashboard is displaying data, **When** the last successful fetch was more than 10 seconds ago, **Then** the data is visually dimmed to indicate staleness
- [ ] **Given** a position has positive unrealised PnL, **When** the positions table renders, **Then** the PnL is displayed in green with absolute value and percentage
- [ ] **Given** a position has negative unrealised PnL, **When** the positions table renders, **Then** the PnL is displayed in red with absolute value and percentage
- [ ] **Given** the Hyperliquid API returns an error on a poll, **When** the dashboard attempts to refresh, **Then** a toast notification is shown and the previous data remains displayed
- [ ] **Given** the Hyperliquid API is persistently unreachable, **When** multiple consecutive polls fail, **Then** an inline error banner is shown above the dashboard content

## Objectives

- Prove authenticated read requests work end-to-end from Hyperliquid testnet through .NET API to Angular UI
- Establish the frontend–backend API contract for account, positions, and orders data
- Implement polling-based auto-refresh with staleness detection and error handling
- Create a tabbed dashboard UI with Angular Material components

### Discovery References

**Architecture decisions (alignment):**
- Angular 19 standalone components (knowledge files + PRD override instruction template)
- Simplified direct DI in controllers (no MediatR/CQRS for POC)
- Angular Material for UI tabs, tables, snackbar, and buttons
- Backend integration tests only (MSTest + Moq + FluentAssertions)
- Assumes F1 is complete — `HyperliquidRestClient`, `HyperliquidSigner`, `HyperliquidOptions`, Angular scaffold all exist

**Key PRD notes:**
- POC has no database, no auth, no MediatR — minimal 2-tier architecture
- `GET /api/orders` lives on `AccountController` for F2; will move to `OrderController` when F5 is implemented
- Hyperliquid REST API uses POST for all queries (including reads) with typed JSON body
- BTC-PERP only — single asset scope

### Project Patterns

- `.agent-context/prd-approved/hyperliquid-poc-prd.md` — PRD defining POC architecture and F2 requirements
- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` — F1 PBI defining the foundation F2 builds on
- `.agent-context/3-develop/backlog/draft/F2-account-dashboard.md` — F2 PBI with full requirements and acceptance criteria
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Hyperliquid API patterns
- `.agent-context/0-knowledge/06-project-structure.md` — Solution layout
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular standalone components guidance
- `.github/instructions/csharp.instructions.md` — C# coding standards (sealed classes, async, CancellationToken)
- `.github/instructions/angular.instructions.md` — Angular conventions (member ordering, observable patterns, SCSS)
- `.github/instructions/api-controllers.instructions.md` — API controller route conventions
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions testing standards

### [x] Phase 1: Backend — DTOs, Service Layer, and API Endpoints

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Create account data DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-11-create-account-data-dtos

- [x] Task 1.2: Create Hyperliquid account service interface and implementation
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-12-create-hyperliquid-account-service

- [x] Task 1.3: Create AccountController with three GET endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-13-create-account-controller

- [x] Task 1.4: Register new services in DI
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-14-register-services-in-di

- [x] Task 1.5: Add backend integration tests for AccountController
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-15-add-backend-integration-tests

- [x] Task 1.6: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-01-details.md#task-16-build-and-run-tests

### [x] Phase 2: Frontend — Angular Material Dashboard with Polling

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Install Angular Material and configure theming
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-21-install-angular-material

- [x] Task 2.2: Create TypeScript models and DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-22-create-typescript-models-and-dtos

- [x] Task 2.3: Extend hyperliquid-api.service.ts with account endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-23-extend-api-service

- [x] Task 2.4: Create DashboardComponent with polling and staleness logic
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-24-create-dashboard-component

- [x] Task 2.5: Create AccountSummaryComponent
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-25-create-account-summary-component

- [x] Task 2.6: Create PositionsTableComponent
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-26-create-positions-table-component

- [x] Task 2.7: Create OrdersTableComponent
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-27-create-orders-table-component

- [x] Task 2.8: Add dashboard route and navigation
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-28-add-dashboard-route-and-navigation

- [x] Task 2.9: Build and lint the frontend
  - Details: .agent-context/3-develop/build/plans/details/20260324-f2-account-dashboard-phase-02-details.md#task-29-build-and-lint-frontend

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — DTOs, Service Layer, and API Endpoints | Medium | Medium |
| Phase 2: Frontend — Angular Material Dashboard with Polling | Medium | Low |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- Medium risk on Phase 1 due to Hyperliquid API response shape uncertainty — the exact JSON fields for `clearinghouseState` need to be matched at implementation time
- Phase 2 is lower risk because Angular Material provides tabs, tables, and snackbar out of the box
- Assumes F1 has established `HyperliquidRestClient` with a working `PostAsync` method for Hyperliquid's POST-based read API
- Tests use `WebApplicationFactory<Program>` for integration testing (adapted from instruction patterns for POC)
- No Angular tests per alignment decision

## Dependencies

- **F1 — Configuration & Connectivity** must be complete (HyperliquidRestClient, HyperliquidOptions, HyperliquidSigner, Angular scaffold, CORS)
- **Nethereum** NuGet package (already installed by F1 for wallet derivation)
- **Angular Material** (`@angular/material`, `@angular/cdk`) — new dependency added in Phase 2
- **MSTest**, **Moq**, **FluentAssertions** NuGet packages — for backend tests
- **Microsoft.AspNetCore.Mvc.Testing** NuGet package — for WebApplicationFactory integration tests

## Success Criteria

- All 12 acceptance criteria from the PBI are satisfied
- `GET /api/account`, `GET /api/positions`, and `GET /api/orders` return correct data from Hyperliquid testnet
- Dashboard auto-refreshes every 2 seconds with correct staleness detection at 10 seconds
- PnL values are color-coded (green/red) with absolute and percentage values
- Toast and inline error handling works for transient and persistent API failures
- Backend integration tests pass for all three endpoints (happy path + error scenarios)
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-24T19:16:09Z | 2026-03-24T19:42:01Z |
| Plan Reviewer | plan-reviewed | 2026-03-24T19:42:45Z | 2026-03-24T19:48:48Z |
| 3-Develop: 2 Implementer | implemented | 2026-03-24T19:50:00Z | 2026-03-24T20:10:00Z |
| 3-Develop: 3 Reviewer | complete | 2026-03-24T21:52:07Z | 2026-03-24T22:14:44Z |
