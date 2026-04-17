<!-- markdownlint-disable-file -->
# Release Changes: Stop Loss & Take Profit

**Related Plan**: 20260329-stop-loss-take-profit-plan.instructions.md
**Implementation Date**: 2026-03-29

## Summary

All three phases completed for backend trigger-order support, frontend order-entry SL or TP workflows, and positions-table SL or TP management.

## Changes

### Added

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- src/TradePilot.Api/Models/PlaceTriggerOrderRequest.cs: Added standalone trigger-order placement request model with DataAnnotations validation.
- src/TradePilot.Api/Models/ModifyTriggerOrderDto.cs: Added trigger-order modification DTO with DataAnnotations validation.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- frontend/trading-ui/src/app/core/models/trigger-order.model.ts: Added trigger-order request and modify model types for the new orders trigger API calls.
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts: Added the standalone SL/TP modal component with typed reactive form validation and liquidation warning logic.
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html: Added the SL/TP modal template for position context, validation messages, and dialog actions.
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss: Added modal styling for the SL/TP form, position context, and liquidation warning.

### Modified

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- src/TradePilot.Api/Models/PlaceOrderRequest.cs: Added optional stop loss and take profit price fields for companion trigger orders.
- src/TradePilot.Api/Models/OpenOrderDto.cs: Added trigger metadata fields for trigger price, TP/SL type, and reduce-only state.
- src/TradePilot.Api/Models/PositionDto.cs: Added stop loss and take profit price and order-id fields for enriched position responses.
- src/TradePilot.Api/Services/IHyperliquidOrderService.cs: Added trigger-order placement and modification service contracts.
- src/TradePilot.Api/Services/HyperliquidOrderService.cs: Implemented trigger-order placement/modification and companion SL/TP submission after successful main orders.
- src/TradePilot.Api/Services/HyperliquidAccountService.cs: Parsed trigger-order details from open orders and enriched positions with correlated SL/TP orders.
- src/TradePilot.Api/Controllers/OrdersController.cs: Added POST, PUT, and DELETE trigger-order endpoints with response metadata and trigger-order lookup logic.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs: Added trigger-order action builder matching Hyperliquid trigger wire format.
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs: Extended modify action typing to support trigger order modifications.
- tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs: Added trigger-order unit coverage for happy path, unknown asset, exchange rejection, and trigger modify flows.
- tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs: Added controller integration coverage for trigger-order endpoints.
- tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs: Added coverage for trigger-order parsing and position enrichment from open trigger orders.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- frontend/trading-ui/src/app/core/models/place-order.model.ts: Added optional stop-loss and take-profit request fields.
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts: Extended the typed reactive form, added SL/TP toggle state, request mapping, side-based validators, partial warning logic, and liquidation-context warning support.
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html: Added the SL/TP toggle UI, SL/TP inputs, validation messages, and non-blocking warnings.
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss: Added styling for the SL/TP section, toggle button, and warning states.
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts: Extended confirm dialog data with optional SL/TP values.
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html: Displayed stop-loss and take-profit rows when present.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- frontend/trading-ui/src/app/core/models/position.model.ts: Extended the position model with nullable stop-loss and take-profit price and order-id fields.
- frontend/trading-ui/src/app/core/models/open-order.model.ts: Extended the open-order model with trigger-order metadata fields.
- frontend/trading-ui/src/app/core/services/order.service.ts: Added place, modify, and cancel trigger-order API methods.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts: Added SL/TP outputs, inline-edit state, and helper methods for displaying and editing trigger prices.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html: Added SL/TP table columns, inline editing controls, remove buttons, and the Set SL/TP action.
- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss: Added styling for SL/TP cells, inline-edit inputs, remove buttons, and action layout.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Wired the SL/TP modal flow and trigger-order set, edit, and remove handlers into the smart dashboard container.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Connected the new SL/TP events from the positions table to the dashboard handlers.

### Removed

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- None.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- None.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- None.

## Test Results

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- OrdersControllerTests + HyperliquidOrderServiceTests + HyperliquidAccountServiceTests: 35/35 passed.
- TradePilot.Domain.Tests: 15/15 passed.
- TradePilot.Application.Tests: 61/61 passed.
- TradePilot.Infrastructure.Tests: 51/51 passed.
- TradePilot.Persistence.Tests: 20/20 passed.
- TradePilot.Api.Tests: 138/138 passed.
- Architecture Tests: NOT RUN — not required by the phase details file.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- Frontend Build: PASSED.
- Frontend Lint: PASSED.
- Architecture Tests: NOT RUN — not required by the phase details file.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- Frontend Build: PASSED.
- Frontend Lint: PASSED.
- Architecture Tests: NOT RUN — not required by the phase details file.

## Issues

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- Running the targeted Debug test/build flow initially failed because the active TradePilot.Api dev process held a lock on Debug output assemblies. Validation was completed in Release configuration instead.
- Build and test runs surfaced an existing NU1903 warning for AutoMapper 12.0.1 vulnerability metadata. This was unrelated to Phase 1 and did not block compilation or tests.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- Angular build reported existing non-blocking bundle budget warnings unrelated to the Phase 2 changes: the initial bundle exceeded the configured 500 kB budget and the backtesting/trade-log-table component SCSS exceeded its configured style budget.
- An intermediate multi-region patch failed due to patch chunk indexing; the implementation was completed with smaller anchored edits.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- Lint initially failed on the new clickable SL or TP values because interactive spans violated accessibility rules. This was resolved by converting them to proper button elements.
- Angular build completed successfully but continued to report the existing non-blocking bundle budget warnings already present in the project: initial bundle size exceeded the configured budget, and the backtesting trade-log-table SCSS exceeded its style budget.

## Design Decisions

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- Used a shared private trigger-order submission path inside HyperliquidOrderService so standalone trigger endpoints and companion SL/TP placement reuse the same signing and submission behavior.
- Preserved existing PlaceOrderAsync success and failure behavior and surfaced companion trigger-order failures as non-blocking warnings in PlaceOrderResponse.Detail, matching the phase tradeoff.
- Added account-service test coverage for trigger parsing and position enrichment because those read-model changes are central to the phase acceptance criteria.
- Ran solution build and tests in Release instead of Debug due locked Debug outputs from the running API process.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- Used optional `stopLossPrice` and `takeProfitPrice` fields in the frontend request model to match the backend Phase 1 contract without changing existing order submission flow.
- Applied SL/TP validation at the form-control level and revalidated on side, order type, price, and live-price updates so errors stay current without introducing a custom form-group error model.
- Used limit price as the reference price for limit orders and live or mark price as the reference for market orders, matching the phase details.
- Implemented the liquidation warning as non-blocking and sourced it from the selected asset’s current open position, when one exists.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- Kept the new SL or TP fields on the frontend Position and OpenOrder models nullable and optional so existing state objects and specs remain compatible while still matching the backend contract.
- Reused the dashboard’s existing smart and dumb split: the positions table only emits SL or TP intents, while the dashboard owns dialog orchestration, API calls, success notifications, and refresh behavior.
- Relied on the existing global HTTP error interceptor for API error notifications to avoid duplicate error toasts, while keeping explicit success notifications in the dashboard handlers.
- Chose cancel-on-blur for inline SL or TP edits because the phase details contained conflicting blur behavior, and the provided implementation snippet specified blur cancellation.

## Review Hints

<!-- Phase 1: Backend — Trigger Order Infrastructure & API -->
- Review the companion-trigger behavior in HyperliquidOrderService, especially the decision to return a successful main order when SL or TP companion placement fails.
- Review the trigger-order correlation rule in HyperliquidAccountService: it currently selects the first `sl` and first `tp` order per asset, which may need refinement if multiple trigger orders per asset become common.

<!-- Phase 2: Frontend — Order Entry with SL/TP -->
- Review the liquidation-warning behavior for new entries on assets without an existing position; the current implementation warns only when a current liquidation price is available from account positions.
- Review whether the confirm dialog should eventually format SL and TP values with a currency prefix for consistency with the acceptance examples.

<!-- Phase 3: Frontend — Positions Table SL/TP Management -->
- Review the partial-success behavior when setting both SL and TP together: the dashboard uses parallel trigger-order requests, so if one succeeds and one fails the refresh will reconcile exchange state but the user will only see a single overall error path.
- Review whether positions with only one of SL or TP set should also expose a direct Set SL or TP action for adding the missing counterpart; the current implementation follows the phase detail wording and only shows that button when both are absent.

## Release Summary

Implemented end-to-end stop loss and take profit support for manual trading. The backend now supports Hyperliquid trigger-order placement, modification, cancellation, and read-time position enrichment from open trigger orders. The order-entry UI now supports optional SL and TP values with side-aware validation, confirmation visibility, and non-blocking warnings. The positions table now displays active SL and TP levels, supports initial setup through a modal, inline editing of existing trigger prices, and removal through trigger-order cancellation. Backend automated tests passed, and frontend build and lint validation passed.