<!-- markdownlint-disable-file -->
# Release Changes: F6 — Order Management

**Related Plan**: 20260325-f6-order-management-plan.instructions.md
**Implementation Date**: 2026-03-25

## Summary

Implements F6 order management across the backend and Angular UI, including Hyperliquid cancel and modify operations, optimistic updates, and user confirmations.

## Changes

### Added

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCancelAction.cs: Added cancel action payload models with Hyperliquid wire-field JSON names.
- src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs: Added batch modify payload models including order type and GTC limit params.
- src/TradingApp.Api/Models/ModifyOrderDto.cs: Added PUT request DTO with positive range validation for price and size.

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- frontend/trading-ui/src/app/core/models/modify-order.model.ts: Added the ModifyOrderDto interface with price and size numeric fields.
- frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts: Added standalone modify-order modal component with typed reactive form and submit and cancel handlers.
- frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html: Added modal template with prefilled fields, validation messages, and action buttons.
- frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.scss: Added BEM-styled modal layout using existing CSS tokens.

### Modified

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- src/TradingApp.Api/Services/IHyperliquidOrderService.cs: Extended service contract with cancel single, cancel all, and modify methods.
- src/TradingApp.Api/Services/HyperliquidOrderService.cs: Implemented cancel/modify flows, added open-order dependency for cancel-all, action submission helper, and input parsing/validation helpers.
- src/TradingApp.Api/Controllers/OrdersController.cs: Added DELETE single, DELETE by asset, and PUT modify endpoints with 204/400/503 response contracts.
- tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs: Added unit tests for cancel/modify behaviors, invalid order id handling, and no-op cancel-all.
- tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs: Added integration tests for cancel/modify success and failure paths, and updated test DI registrations for account service dependency.

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- frontend/trading-ui/src/app/core/services/order.service.ts: Added cancelOrder, cancelAllOrders, and modifyOrder methods and ModifyOrderDto import.

<!-- Phase 3: Frontend — Orders Table Actions + Optimistic UI -->
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts: Added row actions, cancel-all event, loading state APIs, and context-menu wiring.
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html: Added header and cancel-all button, actions column, per-row spinner and disable behavior, and right-click menu.
- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss: Added styles for header, actions, loading row state, and hidden context trigger.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added cancel single, cancel all, and modify handlers with confirmation, optimistic update and revert, toasts, and refresh triggers.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Bound orders table outputs to dashboard handlers.
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts: Extended dialog data contract to support generic message, title, and button text while preserving order-summary mode.
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html: Added generic message rendering and customizable button labels.
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.scss: Added styling for generic confirmation message.

### Removed

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- None.

## Test Results

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- TradingApp.Api.Tests: 40/40 passed
- TradingApp.Infrastructure.Tests: 22/22 passed
- TradingApp.Domain.Tests: 0 tests discovered (project executed, no tests present)
- TradingApp.Application.Tests: 0 tests discovered (project executed, no tests present)
- Architecture Tests: PASSED (no dedicated architecture test project was present in the executed solution scope)

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- Frontend Build: PASSED (npx ng build)
- Frontend Lint: PASSED (npx ng lint)
- Architecture Tests: PASSED — not applicable to this frontend phase scope

<!-- Phase 3: Frontend — Orders Table Actions + Optimistic UI -->
- Frontend Build (npx ng build): PASSED
- Frontend Lint (npx ng lint): PASSED
- Architecture Tests: PASSED — not applicable to this frontend-only phase scope

## Issues

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- Moq expression-tree compile errors occurred in new service tests because JsonSerializer.Serialize was invoked via overloads with optional parameters. Resolved by switching to explicit overload calls with JsonSerializerOptions argument.

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- Initial lint command used a relative Set-Location from an already nested working directory, producing a path warning; reran lint from absolute frontend path and confirmed pass.

<!-- Phase 3: Frontend — Orders Table Actions + Optimistic UI -->
- First build command attempted Set-Location from an already nested frontend working directory and printed a path warning; build still completed successfully, then lint was run from the active frontend directory and passed.

## Design Decisions

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- Reused the existing EIP-712 signing pipeline from F5 for cancel/modify by extracting a shared action submission helper in the order service.
- Kept BTC asset index hard-coded to 0 per POC scope and phase requirements.
- Used DomainException for invalid order id and missing asset query handling to align with existing global exception-to-400 behavior.
- Resolved modify side and asset from current open orders in controller before invoking service, matching the phase detail contract.

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- None — implemented exactly as specified in the phase details, including method signatures, endpoint paths, modal structure, and validation rules.

<!-- Phase 3: Frontend — Orders Table Actions + Optimistic UI -->
- Extended the existing confirm dialog to support both existing order-entry confirmation and new generic cancel and cancel-all confirmations, avoiding duplication of dialog components.
- Kept optimistic behavior as specified: cancel operations remove orders immediately and restore on API failure; modify updates values immediately and reverts on API failure.
- Used OrdersTableComponent row and global loading APIs from DashboardComponent via ViewChild to keep mutation flow in the parent while preserving row-level UX state.

## Review Hints

<!-- Phase 1: Backend — Cancel & Modify Endpoints + Tests -->
- Validate that resolving order side and asset from open orders in src/TradingApp.Api/Controllers/OrdersController.cs aligns with expected behavior when exchange state is stale between read and modify.
- Consider whether asset normalization rules in src/TradingApp.Api/Services/HyperliquidOrderService.cs should be centralized further as additional assets are introduced beyond POC BTC-only scope.

<!-- Phase 2: Frontend — Order Management Service + Modify Modal -->
- Verify modal wiring in the next phase integration points (orders table actions) to ensure dialog data shape and returned ModifyOrderDto are consumed without additional mapping.

<!-- Phase 3: Frontend — Orders Table Actions + Optimistic UI -->
- Cancel single uses optimistic removal before API completion, so row spinner visibility is limited for that path by design; verify this UX aligns with the expected interpretation of row-level loading for cancel actions.
- Confirm that generic confirm-dialog text changes do not conflict with desired wording in existing order-entry flows.

## Release Summary

Implemented the full F6 order-management slice across backend and frontend. The API now supports signed cancel single, cancel all, and modify operations against Hyperliquid testnet, and the Angular dashboard now exposes row actions, a scoped cancel-all action, right-click context menu actions, confirmation dialogs, optimistic UI updates with revert-on-failure behavior, row and global loading indicators, and toast feedback. Backend API and infrastructure tests passed, and frontend build and lint passed for the completed feature.