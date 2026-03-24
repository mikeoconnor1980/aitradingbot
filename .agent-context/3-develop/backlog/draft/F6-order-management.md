# PBI Specification: F6 — Order Management

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 5
**Risk Level:** Medium
**Depends On:** F1, F2, F5

---

## Summary

Cancel and modify existing orders on Hyperliquid testnet, proving that cancel/modify signing works and the UI can manage the full order lifecycle.

### User Story

> As a **developer**, I want to **cancel and modify existing orders** so that **I can manage my open positions and correct mistakes without needing to interact directly with the exchange API**.

### Business Value

Extends the signing validation from F5 to cover cancel and modify actions. The production trading engine needs all three operations — proving them in the POC confirms the signing approach is complete.

---

## Problem Statement

After placing orders (F5), users need the ability to manage their order lifecycle — cancelling individual orders, bulk-cancelling all orders for an asset, and modifying price/size on existing orders. All actions must be available from the Angular UI with confirmation dialogs to prevent accidental destructive operations.

---

## Requirements

### Functional Requirements

1. Cancel a single order by order ID via the Hyperliquid API
2. Cancel all open orders for the currently selected asset
3. Modify an existing order's price and/or size in a single operation
4. Cancel actions available from multiple entry points: orders table row button, order detail view, and context menu
5. "Cancel All" button scoped to the currently viewed asset (BTC-PERP in POC)
6. Modify action opens a modal dialog pre-filled with the current order values (price, size)
7. Confirmation dialog shown before all destructive actions (single cancel, Cancel All, and modify)
8. Optimistic UI update: orders table updates immediately, reverts if the API call fails
9. Toast/snackbar notifications for success and error feedback (auto-dismissing)
10. Row-level loading state: action buttons disabled with spinner while API call is in flight (prevents double-clicks)
11. Frontend validation on modify dialog (price > 0, size > 0, within exchange limits)
12. Backend validation before sending to Hyperliquid (duplicate of frontend rules as safety net)
13. On failure (e.g., order already filled, network error): revert optimistic update and show error toast

### Non-Functional Requirements

- All cancel/modify operations must use EIP-712 signed requests (reuses signing from F5)
- Cancel and modify API calls should complete within 2 seconds under normal network conditions
- UI must remain responsive during API calls (non-blocking with loading indicators)
- All errors logged with structured logging (Serilog)

---

## User Flow

### Happy Path — Cancel Single Order

1. User has open orders visible in the dashboard orders table (from F2)
2. User clicks "Cancel" button on an order row (or via context menu / detail view)
3. Confirmation dialog appears: "Cancel order {orderId}?"
4. User confirms
5. Order is optimistically removed from the table; cancel request signed and submitted
6. Success toast shown
7. If API fails, order reappears and error toast shown

### Happy Path — Cancel All

1. User clicks "Cancel All" button above the orders table
2. Confirmation dialog appears: "Cancel all N open orders for BTC-PERP?"
3. User confirms
4. All orders optimistically removed; cancel requests signed and submitted
5. Success toast shown
6. If API fails, orders reappear and error toast shown

### Happy Path — Modify Order

1. User clicks "Modify" on an order row (or via context menu / detail view)
2. Modal dialog opens pre-filled with current price and size
3. User edits price and/or size; frontend validation runs on input
4. User submits the modification
5. Order is optimistically updated in the table; modify request signed and submitted
6. Success toast shown
7. If API fails, order reverts to previous values and error toast shown

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Order already filled before cancel | Revert optimistic update; error toast: "Order already filled" |
| Order already cancelled | Revert optimistic update; error toast with appropriate message |
| Invalid modify parameters (e.g. size ≤ 0) | Frontend validation prevents submission |
| Backend validation failure | Error response surfaced via error toast |
| Signing error on cancel/modify | Clear error message; same diagnostic path as F5 signing errors |
| Network error during cancel/modify | Revert optimistic update; error toast with network error message |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| DELETE | `/api/orders/{orderId}` | Cancel a single order by ID |
| DELETE | `/api/orders?asset={asset}` | Cancel all open orders for an asset |
| PUT | `/api/orders/{orderId}` | Modify an existing order (price and/or size) |

### Modify Request Body

```json
{
  "price": 64500.00,
  "size": 0.002
}
```

### Key Components

| Component | Action |
|-----------|--------|
| `OrderController` | DELETE and PUT endpoints for cancel/modify |
| `HyperliquidSigner` | Signs cancel and modify actions (different type structures from place) |
| `HyperliquidRestClient` | Submits signed cancel/modify requests |
| `hyperliquid-api.service.ts` | Angular service calling cancel/modify endpoints |
| Dashboard orders table | Cancel button per row, Cancel All button, modify action, context menu |

### Signing Considerations

Cancel and modify likely use different EIP-712 type structures than order placement. The `HyperliquidSigner` must support:
- Order placement type (proven in F5)
- Cancel type (order ID + nonce)
- Modify type (order ID + new parameters + nonce)

Each type must have its own domain-compatible type hash.

### Tech Stack Compliance

- **Backend**: .NET 8 Web API — cancel/modify endpoints added to existing `OrderController`
- **Signing**: Reuses `HyperliquidSigner` (EIP-712) from F5 for cancel and modify operations
- **Frontend**: Angular 19 standalone — modal dialog component, context menu, toast service
- **State**: In-memory only (no database) — order state refreshed from Hyperliquid API
- **No new dependencies required** — fully within approved POC tech stack

---

## Out of Scope

- Batch modification of multiple orders at once
- Order history / audit trail
- Undo after cancel (order must be re-placed)
- Cancel/modify for orders on assets other than the currently viewed one
- Partial fill handling during modification (deferred to F7/F8)
- Drag-to-modify on a price chart

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** I have an open order in the orders table, **When** I click the cancel button on the order row, **Then** a confirmation dialog appears asking me to confirm the cancellation
- [ ] **Given** the confirmation dialog is shown for a single cancel, **When** I confirm, **Then** the order is removed from the table optimistically and a cancel request is sent to Hyperliquid
- [ ] **Given** the confirmation dialog is shown for a single cancel, **When** I dismiss/cancel the dialog, **Then** no action is taken and the order remains
- [ ] **Given** I have open orders for BTC-PERP, **When** I click "Cancel All", **Then** a confirmation dialog appears stating the number of orders to be cancelled
- [ ] **Given** I confirm Cancel All, **When** the API call succeeds, **Then** all orders for BTC-PERP are removed from the table and a success toast is shown
- [ ] **Given** I have an open order, **When** I click "Modify", **Then** a modal dialog opens pre-filled with the order's current price and size
- [ ] **Given** I am in the modify dialog, **When** I enter a valid new price and/or size and confirm, **Then** the order is updated optimistically and a modify request is sent to Hyperliquid
- [ ] **Given** I enter an invalid value in the modify dialog (e.g., price ≤ 0), **When** I try to submit, **Then** a validation error is shown and the form does not submit
- [ ] **Given** a cancel or modify API call fails, **When** the error response is received, **Then** the optimistic update is reverted and an error toast is shown with a meaningful message
- [ ] **Given** an API call is in flight for a specific order, **When** I look at that order row, **Then** the action buttons are disabled and a spinner is shown on the row
- [ ] **Given** I right-click on an order row, **When** the context menu appears, **Then** I see cancel and modify options
- [ ] **Given** I am in an order detail view, **When** I click the cancel button, **Then** the same confirmation and cancel flow is triggered as from the table row

### Release Notes Information

- **Heading**: Order Cancel & Modify on Hyperliquid Testnet
- **Release note type**: Feature
- **Release Note Summary**: Cancel and modify existing orders on Hyperliquid testnet via the Angular UI, with EIP-712 signing for cancel/modify actions, optimistic UI updates, and confirmation dialogs.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F2** — Orders table in the dashboard is the UI surface for management actions
- **F5** — Order placement must work before cancel/modify can be tested
- **F7** — Order update events from WebSocket will also update the orders table
