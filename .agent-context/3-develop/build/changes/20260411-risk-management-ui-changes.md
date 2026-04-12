<!-- markdownlint-disable-file -->
# Release Changes: Risk Management UI - R-Based Position Sizing

**Related Plan**: 20260411-risk-management-ui-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

Completed implementation of the risk management UI for RiskBased sizing, auto-leverage, and live preview support. The form infrastructure, risk card behavior, live preview, and backtest/summary surfaces are in place, and the frontend workspace now verifies cleanly after fixing the unrelated pre-existing lint and spec drift that had been blocking final sign-off.

## Changes

### Added

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts: Added unit coverage for sizing-mode visibility, auto-leverage behavior, warning messaging, stop-loss requirement messaging, and control error reporting.

### Modified

<!-- Phase 1: TypeScript Models & Form Infrastructure -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added `risk_based` to the sizing union and added `riskPerTradePercent` plus `autoLeverage` to `RiskConfig`.
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added RiskBased fields to `BacktestRiskConfig`.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added `riskPerTradePercent` and `autoLeverage` controls with defaults and validators to the risk form group.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Passed the exit form group into the risk management card.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Mapped RiskBased-only risk fields conditionally for save and backtest payloads.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Added mode-conditional validation for `positionSizeValue`, `riskPerTradePercent`, and fixed-percent stop-loss requirements.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts: Added an optional `exitGroup` input so the parent template binding compiles ahead of the fuller card implementation.

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts: Added reactive state management, conditional visibility logic, and leverage control synchronization for RiskBased sizing and auto-leverage.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html: Added the RiskBased sizing option, conditional inputs, warning banner, and stop-loss guidance messaging.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss: Added layout and styling for the new toggle, warning, and validation sections.

<!-- Phase 3: Live Calculation Preview -->
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts: Added account equity loading, reactive preview calculation, preview state handling, and fixed-percent stop-loss gating for the live RiskBased preview.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html: Added the live preview panel with prerequisite and guidance states.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss: Added preview-panel layout and responsive styling.
- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts: Extended the card spec with preview calculation, no-equity, and missing-stop-loss coverage.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts: Added a mocked HyperliquidApiService provider so the updated risk card dependency does not break page-level tests.

<!-- Phase 4: Backtest & Preview Summary Updates -->
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Added a `risk_based` branch to the position size label.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts: Added a `risk_based` branch to the position size label.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts: Added RiskBased summary text including auto-leverage and manual leverage display.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts: Added RiskBased label coverage and aligned risk fixtures with the expanded risk shape.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.spec.ts: Added RiskBased label coverage and aligned risk fixtures with the expanded risk shape.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts: Updated mock configs and assertions for the new disabled-by-default RiskBased controls.

<!-- Verification Cleanup -->
- frontend/trading-ui/src/app/app.component.spec.ts: Updated layout mocking and sidebar assertions so shell tests match the current desktop navigation structure.
- frontend/trading-ui/src/app/core/components/help-panel.component.html: Reworked the dismiss affordance to satisfy accessibility lint rules.
- frontend/trading-ui/src/app/core/pipes/help-markdown.pipe.ts: Switched the pipe to `inject()`-based DI to satisfy Angular lint guidance.
- frontend/trading-ui/src/app/core/services/responsive-dialog.service.ts: Replaced loose `any` typing with `unknown` in dialog config handling.
- frontend/trading-ui/src/app/features/agents/kill-switch-dialog.component.ts: Removed an unused Material dialog data import flagged by lint.
- frontend/trading-ui/src/app/features/connection/status-card.component.spec.ts: Added router setup so status-card specs resolve `ActivatedRoute` correctly under test.
- frontend/trading-ui/src/app/features/dashboard/market-context-card/market-context-card.component.ts: Removed an unused local variable reported by lint.
- frontend/trading-ui/src/app/features/dashboard/orders-table/order-card/order-card.component.html: Replaced loose template equality checks with strict comparisons.
- frontend/trading-ui/src/app/features/dashboard/positions-table/position-card/position-card.component.html: Replaced loose template equality checks with strict comparisons.
- frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.html: Reworked decorative labels and template comparisons to satisfy form-accessibility and equality lint rules.
- frontend/trading-ui/src/app/features/optimizer/optimizer-detail/optimizer-detail.component.html: Replaced loose template equality checks with strict comparisons.
- frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.html: Replaced loose template equality checks with strict comparisons.
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html: Replaced loose template equality checks in disabled-state logic.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Aligned the expected stop-loss shape with the current mapper output including `trailingStopWarmup`.

### Removed

## Test Results

<!-- Phase 1: TypeScript Models & Form Infrastructure -->
- Angular build (`npm run build`): PASSED.
- Angular lint (`npm run lint`): PASSED after frontend verification cleanup.

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- RiskManagementCardComponent targeted spec: 16/16 passed.
- Angular build: PASSED.
- Angular lint: PASSED after frontend verification cleanup.
- Angular full test suite: PASSED after frontend verification cleanup.

<!-- Phase 3: Live Calculation Preview -->
- RiskManagementCardComponent targeted spec: 23/23 passed.
- StrategyBuilderPageComponent targeted spec: 7/7 passed.
- Angular build: PASSED.
- Angular lint: PASSED after frontend verification cleanup.
- Angular full test suite: PASSED after frontend verification cleanup.

<!-- Phase 4: Backtest & Preview Summary Updates -->
- Targeted Angular specs (`backtest-form`, `backtest-result`, `strategy-builder-page`): 27/27 passed.
- Angular build: PASSED.
- Angular lint: PASSED.
- Angular full test suite: 206/206 passed.

## Issues

<!-- Phase 1: TypeScript Models & Form Infrastructure -->
- The new parent binding required the risk card component to accept `exitGroup`; a minimal optional input was added in Phase 1 to keep the template compiling without pulling Phase 2 behavior forward.

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- The full frontend workspace is now green; the earlier unrelated lint and spec drift issues were corrected during final verification cleanup.

<!-- Phase 3: Live Calculation Preview -->
- The updated standalone risk card now injects HyperliquidApiService, which initially broke StrategyBuilderPageComponent tests until the page spec was updated with a mock provider.
- No remaining Phase 3-specific issues after verification cleanup.

<!-- Phase 4: Backtest & Preview Summary Updates -->
- The page-level strategy builder spec initially used `FormGroup.contains()` for the new controls, but those controls are disabled by default in percent-wallet mode; the spec was corrected to assert existence via `get()` and expected disabled state.
- No remaining Phase 4-specific issues after verification cleanup.

## Design Decisions

<!-- Phase 1: TypeScript Models & Form Infrastructure -->
- Added the `exitGroup` input to the risk card as a compatibility shim in Phase 1 so the parent binding could land without prematurely implementing the preview logic.
- Kept `riskPerTradePercent` and `autoLeverage` serialized only for `risk_based` mode to avoid emitting irrelevant fields for existing sizing modes.

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- Used reactive enable and disable logic in the component class instead of template-only hiding so the form model stays aligned with the visible controls.
- Kept Phase 2 scoped to structural card UI and unit tests without pulling preview calculation logic forward from Phase 3.

<!-- Phase 3: Live Calculation Preview -->
- The preview fetches equity once on init and recalculates locally from form state to keep the UI responsive without introducing a backend calculation dependency.
- The preview is intentionally limited to fixed-percent stop-loss inputs to avoid showing misleading calculations for unsupported stop-loss types.
- The maintenance margin rate remains hardcoded to 0.01, matching the current knowledge-doc assumption for BTC at 50x preview behavior.

<!-- Phase 4: Backtest & Preview Summary Updates -->
- Used a consistent `R-based (X% risk)` label format across backtest form and result surfaces, with a fallback of `1%` when older configs omit the field.
- Kept non-RiskBased summary and label behavior unchanged so the new UI remains additive.

## Review Hints

<!-- Phase 1: TypeScript Models & Form Infrastructure -->
- Review `strategy-validation.service.ts` closely because later UI phases depend on the new client-side rule that `risk_based` requires a fixed-percent stop-loss.
- Review `strategy-mapper.service.ts` to confirm the RiskBased-only payload fields match the backend contract assumptions.

<!-- Phase 2: Risk Management Card UI & Unit Tests -->
- Review `risk-management-card.component.ts` for the interaction between sizing mode and leverage control state, since the preview logic in the next phase builds on that behavior.
- Review `risk-management-card.component.spec.ts` because it is the main proof that the conditional DOM and validation messaging behave correctly for the new UI states.

<!-- Phase 3: Live Calculation Preview -->
- Review `risk-management-card.component.ts` for the preview math, reset behavior, and prerequisite gating order.
- Review `risk-management-card.component.html` to confirm the preview message precedence is correct when wallet connection or stop-loss prerequisites are missing.

<!-- Phase 4: Backtest & Preview Summary Updates -->
- Review `preview-summary-card.component.ts` to confirm the new RiskBased wording is what you want for auto-leverage versus manual-leverage cases.
- Review the updated backtest label specs to confirm the default `1%` fallback is acceptable for older saved configurations.

## Release Summary

Implemented the full RiskBased risk-management UI flow, including form model updates, conditional RiskBased controls, auto-leverage behavior, live account-equity preview calculations, and RiskBased wording across backtest and summary surfaces. Final verification is now clean with Angular build passing, `npm run lint` passing, and the full headless Angular suite passing at 206/206.