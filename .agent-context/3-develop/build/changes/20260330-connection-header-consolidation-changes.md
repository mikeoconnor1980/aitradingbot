<!-- markdownlint-disable-file -->
# Release Changes: Consolidate Connection Indicator into Connection Pill

**Related Plan**: 20260330-connection-header-consolidation-plan.instructions.md
**Implementation Date**: 2026-03-30

## Summary

Implements the header connection control consolidation by removing the redundant nav link and making the existing status pill the single navigation entry point to the connection page.

## Changes

### Added

### Modified

<!-- Phase 1: Consolidate Header Connection Elements -->
- frontend/trading-ui/src/app/app.component.html: Removed the redundant Connection nav link and converted the status pill into a link to /connection with the updated accessibility label.
- frontend/trading-ui/src/app/app.component.scss: Added interactive link styling for the clickable status pill without changing its status color behavior.
- frontend/trading-ui/src/app/app.component.spec.ts: Replaced the stale HealthService mock with a SignalRService mock and added coverage for the 4-link nav and clickable status pill.

### Removed

## Test Results

<!-- Phase 1: Consolidate Header Connection Elements -->
- Angular frontend test suite: 91/91 passed
- Frontend build: PASSED - existing bundle budget warnings only
- Frontend lint: PASSED
- Architecture Tests: PASSED - not applicable for this frontend-only phase

## Issues

<!-- Phase 1: Consolidate Header Connection Elements -->
- The existing AppComponent spec was mocking HealthService even though the component depends on SignalRService. Resolved by updating the test provider to use a SignalRService mock.
- The first terminal attempt to run the Angular tests closed the shared shell due to the command shape. Resolved by rerunning with a non-terminating PowerShell command.
- The frontend build reported existing budget warnings, but the build completed successfully and no code changes were required for this phase.

## Design Decisions

<!-- Phase 1: Consolidate Header Connection Elements -->
- Kept frontend/trading-ui/src/app/app.component.ts unchanged because RouterLink was already imported, so no class-level change was necessary.
- Updated the status pill in place rather than wrapping it in another element to preserve the existing status styling and keep the DOM structure minimal.
- Used a focused AppComponent spec update instead of broader router or navigation test infrastructure because the phase acceptance criteria are satisfied by asserting rendered link structure and href output.

## Review Hints

<!-- Phase 1: Consolidate Header Connection Elements -->
- Review the header interaction in the browser to confirm the clickable pill still reads clearly as a status indicator while also feeling like navigation.
- Review the updated AppComponent spec to confirm the intended contract is now the single connection entry point: no nav link, 4 primary nav items, and a status pill linking to /connection.

## Release Summary

Implemented the single planned phase for this PBI. The header now exposes connection state through one control by removing the redundant Connection nav item and making the existing status pill the navigation link to /connection. Frontend tests, build, and lint all completed successfully; the only build output issue was pre-existing bundle budget warnings.
