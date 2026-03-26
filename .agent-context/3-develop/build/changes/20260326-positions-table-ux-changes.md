<!-- markdownlint-disable-file -->
# Release Changes: F10 — Positions Table UX Enhancements

**Related Plan**: 20260326-positions-table-ux-plan.instructions.md
**Implementation Date**: 2026-03-26

## Summary

Client-side Angular enhancements to the trading dashboard: Cross Margin Ratio progress bar with threshold-based colour coding and pulsing animation, sortable/filterable positions table with ascending/descending/none sort cycle, and a sequential Close All Positions flow with confirmation dialog, progress tracking, and partial-failure handling.

## Changes

### Added

<!-- Phase 1: Cross Margin Ratio Visual Indicator -->
- frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.ts: New standalone indicator component with threshold logic, tooltip label mapping, and percentage calculation.
- frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.html: New template rendering progress bar, numeric ratio value, and critical warning icon.
- frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.scss: New BEM styles for threshold colors and critical pulse animation.
- frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.spec.ts: New unit tests for thresholds, capping, warning icon visibility, and critical class application.

<!-- Phase 2: Column Sorting & Asset Filter -->
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts: Unit tests covering sort cycle, column switching, filtering, combined filter+sort behavior, and filter state helpers.

<!-- Phase 3: Close All Positions -->
- frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.ts: New standalone dialog component with typed dialog data/result and confirm/cancel actions.
- frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.html: New dialog template listing positions and confirm/cancel controls.
- frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.scss: New BEM-style dialog styling for list rows and side coloring.
- frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.spec.ts: Unit tests for rendering and confirm/cancel result behavior.
- frontend/trading-ui/src/app/core/services/order.service.spec.ts: Unit tests for sequential close-all progress and failure handling.

### Modified

<!-- Phase 1: Cross Margin Ratio Visual Indicator -->
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts: Imported and registered MarginRatioIndicatorComponent in standalone imports.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html: Replaced raw cross margin ratio value with indicator component binding.
- frontend/trading-ui/src/styles.scss: Added global CSS tokens for warning and elevated-warning threshold colors.

<!-- Phase 2: Column Sorting & Asset Filter -->
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts: Added sort/filter state, computed sorted+filtered getter, sort cycling, filter handlers, and required Material imports.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html: Added filter toolbar with clear button and result count, sortable headers with direction icons, filtered empty state, and switched row iteration to sortedFilteredPositions.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss: Added styles for toolbar, filter count, sortable header affordance, sort icons, and filtered empty state.

<!-- Phase 3: Close All Positions -->
- frontend/trading-ui/src/app/core/models/place-order.model.ts: Added CloseAllProgress interface.
- frontend/trading-ui/src/app/core/services/order.service.ts: Added sequential closeAllPositions() with per-item error capture and progress scan.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts: Added closeAllPositions output, globalLoading state, and setGlobalLoading().
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html: Added Close All button in toolbar with loading/disabled handling.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss: Added styles for Close All button and toolbar wrapping.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added onCloseAllPositions() flow with dialog confirm, optimistic clear, sequential close, summary toasts, rollback on all-fail/error.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Bound closeAllPositions event from positions table.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts: Added Close All button visibility/disable/emit tests.

### Removed

## Test Results

<!-- Phase 1: Cross Margin Ratio Visual Indicator -->
- MarginRatioIndicatorComponent: 7/7 passed
- Frontend full test suite: 11/11 passed
- Frontend build: PASSED (existing bundle budget warning present, not introduced by this phase)
- Frontend lint: PASSED

<!-- Phase 2: Column Sorting & Asset Filter -->
- PositionsTableComponent sorting/filtering tests: 12/12 passed
- Frontend full unit test suite: 23/23 passed
- Frontend build: PASSED (existing bundle budget warning unchanged)
- Frontend lint: PASSED

<!-- Phase 3: Close All Positions -->
- Angular unit tests (all components + services): 33/33 passed
- Frontend build: PASSED (existing bundle budget warning only)
- Frontend lint: PASSED

## Issues

<!-- Phase 1: Cross Margin Ratio Visual Indicator -->
- SignalR negotiation console errors appear during tests but all tests passed — pre-existing issue.
- Existing Angular bundle budget warning on build — pre-existing, not introduced by this phase.

<!-- Phase 2: Column Sorting & Asset Filter -->
- Shell working directory shift caused one verification step to use an incorrect relative path; re-ran from explicit frontend path — no code impact.
- Existing Angular bundle budget warning remained unchanged.

<!-- Phase 3: Close All Positions -->
- First test command invocation terminated due to terminal session closure; re-ran successfully — no code impact.
- Existing SignalR negotiation console errors still appear during test run output but test suite passed.

## Design Decisions

<!-- Phase 1: Cross Margin Ratio Visual Indicator -->
- Used CSS custom properties for warning colors in global styles so the indicator uses shared design tokens rather than hardcoded component-only color values.
- Kept threshold and critical-state logic in computed getters on the component class for deterministic, testable behavior.

<!-- Phase 2: Column Sorting & Asset Filter -->
- Implemented sorting/filtering as a computed getter to keep template rendering declarative and avoid mutating the original input array.
- Applied filter before sort in the getter so combined behavior matches requirements.
- Sorting scoped to five specified columns; descending-first cycle with third-click reset to original API order.

<!-- Phase 3: Close All Positions -->
- Kept CloseAllDialogComponent as a confirm-first dialog and orchestrated the async close sequence in DashboardComponent to match existing cancel-all/close-position patterns.
- Implemented sequential dispatch in OrderService using concat + scan with per-request catchError to avoid aborting on single failures and to preserve nonce-safe ordering.
- Used final progress emission in DashboardComponent to drive summary notifications and rollback behavior.

## Review Hints

- Verify the visual styling of Angular Material progress bar colors in the running UI — Material internals can vary by version/theme.
- Confirm tooltip content rendering behavior in browser (tests validate threshold labels and class behavior; tooltip overlay content itself is not deeply asserted).
- Verify UX behavior in browser for repeated header clicks on the same column and cross-column sort switching.
- Verify filtered empty-state message text and result counter updates while typing and when clearing filter.
- Verify UX behavior manually for full success, partial failure, and all-failed close-all scenarios in the running dashboard.
- Verify that failed positions remain visible after refresh in partial failure cases.
- Confirm button disabling/spinner behavior during global loading in the positions tab.

## Release Summary

All 3 phases of F10 — Positions Table UX Enhancements implemented successfully.

**Phase 1 — Cross Margin Ratio Visual Indicator**: New `MarginRatioIndicatorComponent` with a four-threshold colour-coded progress bar (green/yellow/orange/red), pulsing critical animation, tooltips, and global CSS custom property tokens. Integrated into `AccountSummaryComponent`. 7 unit tests pass.

**Phase 2 — Column Sorting & Asset Filter**: `PositionsTableComponent` gains sortable column headers cycling ascending → descending → none, a computed `sortedFilteredPositions` getter, a live filter toolbar with result count and clear button, and filtered empty state. 12 unit tests pass.

**Phase 3 — Close All Positions**: New `CloseAllDialogComponent` for confirmation; `OrderService.closeAllPositions()` dispatches sequentially via RxJS `concat + scan`; `DashboardComponent.onCloseAllPositions()` orchestrates dialog, optimistic UI clear, progress toasts, partial-failure handling, and rollback. `CloseAllProgress` interface added to the model. 33 unit tests pass across all phases.

**Total**: 4 new component files + 1 new spec for order service created; 11 existing files modified; 0 files removed. All builds and lints pass. No regressions.
