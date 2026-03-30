applyTo: ".agent-context/3-develop/build/changes/20260330-connection-header-consolidation-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-30T20:08:29Z"
status: "implemented"
lastUpdated: "2026-03-30T20:12:37Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Consolidate Connection Indicator into Connection Pill

## Overview

Remove the "Connection" navigation link from the header nav bar and make the existing status pill (right side) clickable to navigate to the `/connection` page, consolidating two redundant connection elements into one.

## PBI Details (If Applicable)

As a **user**, I want **the connection status indicator consolidated into the existing connection pill (on the right side of the header)** so that **the header is cleaner and connection state is managed from a single control**.

### Acceptance Criteria

- [ ] **Given** the header is rendered, **When** the user looks at the header, **Then** there is no separate "Connection" nav link — only the status pill
- [ ] **Given** the user is connected, **When** they view the pill, **Then** it shows a connected state with green indicator
- [ ] **Given** the connection drops, **When** the pill updates, **Then** it shows a disconnected state with red indicator
- [ ] **Given** the user clicks the status pill, **When** the navigation completes, **Then** they are taken to the `/connection` page showing full system/connection information

## Objectives

- Remove the "Connection" `<a>` from the header navigation bar
- Make the status pill (`app-shell__status`) a clickable link that navigates to `/connection`
- Maintain all existing colour coding (green/amber/red) and status text on the pill
- Keep the `/connection` route and `StatusCardComponent` page unchanged
- Update tests to reflect the removed nav link and new clickable pill

### Discovery References

- The header has two connection-related elements: a "Connection" nav link in `<nav>` and a status pill (`app-shell__status`) on the right
- The pill is already fully colour-coded (green=Connected, amber=Reconnecting, red=Disconnected) via `SignalRService.connectionStatus$`
- The "Connection" nav link navigates to `/connection` route → `StatusCardComponent` (full-page health card with Refresh button)
- No separate "bubble component" exists — the nav link IS the element to remove

### Project Patterns

- `frontend/trading-ui/src/app/app.component.html` - Header template with nav links and status pill
- `frontend/trading-ui/src/app/app.component.ts` - AppComponent with `statusClass` getter and SignalR subscription
- `frontend/trading-ui/src/app/app.component.scss` - Pill styling with `.status--connected/reconnecting/disconnected`
- `frontend/trading-ui/src/app/app.component.spec.ts` - AppComponent tests (Karma/Jasmine)
- `frontend/trading-ui/src/app/app.routes.ts` - Route definitions including `/connection`

### [x] Phase 1: Consolidate Header Connection Elements

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Remove "Connection" nav link from header template
  - Details: .agent-context/3-develop/build/plans/details/20260330-connection-header-consolidation-phase-01-details.md#task-11-remove-connection-nav-link

- [x] Task 1.2: Make status pill clickable with navigation to /connection
  - Details: .agent-context/3-develop/build/plans/details/20260330-connection-header-consolidation-phase-01-details.md#task-12-make-status-pill-clickable

- [x] Task 1.3: Add hover/cursor styles for clickable pill
  - Details: .agent-context/3-develop/build/plans/details/20260330-connection-header-consolidation-phase-01-details.md#task-13-add-hover-cursor-styles

- [x] Task 1.4: Update AppComponent tests
  - Details: .agent-context/3-develop/build/plans/details/20260330-connection-header-consolidation-phase-01-details.md#task-14-update-appcomponent-tests

- [x] Task 1.5: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260330-connection-header-consolidation-phase-01-details.md#task-15-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|----------|------|
| Phase 1: Consolidate Header Connection Elements | Low | Low |
| **Total** | Low | Low |

### Scoping Notes

- This is purely a UI consolidation — no backend, service, or connection logic changes
- The `/connection` route and `StatusCardComponent` are not modified
- The `SignalRService` and `HealthService` remain unchanged

## Dependencies

- Angular Router (`routerLink` directive, already imported)

## Success Criteria

- Header shows only 4 nav links: Dashboard, Market Data, Order Entry, Backtesting
- Status pill on the right is clickable and navigates to `/connection`
- All existing colour coding and status text continue to work
- Frontend builds and lints without errors
- All existing tests pass, updated tests cover the new behaviour

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-03-30T19:44:47Z | 2026-03-30T19:56:33Z |
| Plan Reviewer | plan-reviewed | 2026-03-30T20:02:05Z | 2026-03-30T20:05:30Z |
| Plan Implementer | implemented | 2026-03-30T20:08:29Z | 2026-03-30T20:12:37Z |
