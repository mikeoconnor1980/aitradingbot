# PBI Specification: F6 — Order Management

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 5  
**Risk Level:** Medium  
**Depends On:** F1, F2, F5

---

## Summary

Cancel and modify existing orders on Hyperliquid testnet, proving that cancel/modify signing works and the UI can manage the order lifecycle.

### User Story

> As a **developer**, I want to **cancel and modify existing orders** so that **I can prove the full order lifecycle (place → modify → cancel) works end-to-end**.

### Business Value

Extends the signing validation from F5 to cover cancel and modify actions. The production trading engine needs all three operations — proving them in the POC confirms the signing approach is complete.

---

## Requirements

### Functional Requirements

- [ ] Cancel a single order by order ID
- [ ] Cancel all open orders
- [ ] Modify an existing order (change price or size)
- [ ] Angular UI shows cancel button per order row in the orders table
- [ ] Angular UI shows "Cancel All" button
- [ ] Confirmation dialog before destructive actions (cancel, cancel all)
- [ ] Orders table updates after cancel/modify without manual refresh

### Non-Functional Requirements

- [ ] Cancel and modify use EIP-712 signing (same as order placement)
- [ ] Confirmation dialog is a simple browser confirm or lightweight modal

---

## User Flow

### Happy Path — Cancel Single Order

1. Developer has open orders visible in the dashboard orders table (from F2)
2. Developer clicks "Cancel" button on an order row
3. Confirmation dialog appears: "Cancel order {orderId}?"
4. Developer confirms
5. Cancel request signed and submitted
6. Order disappears from orders table
7. Success message shown in UI

### Happy Path — Cancel All

1. Developer clicks "Cancel All" button above the orders table
2. Confirmation dialog appears: "Cancel all open orders?"
3. Developer confirms
4. All cancel requests signed and submitted
5. Orders table clears
6. Success message shown in UI

### Happy Path — Modify Order

1. Developer clicks "Modify" on an order row
2. Inline edit or modal allows changing price and/or size
3. Developer submits the modification
4. Modify request signed and submitted
5. Orders table shows updated order details
6. Success message shown in UI

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Order already filled before cancel | Exchange returns error; UI shows "order not found / already filled" |
| Order already cancelled | Exchange returns error; UI shows appropriate message; table refreshed |
| Invalid modify parameters (e.g. size 0) | Frontend validation prevents submission |
| Signing error on cancel/modify | Clear error message; same diagnostic path as F5 signing errors |
| Network error during cancel | Error surfaced; order state unknown — dashboard re-fetches on next poll |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| DELETE | `/api/orders/{orderId}` | Cancel a single order |
| DELETE | `/api/orders` | Cancel all open orders |
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
| Dashboard orders table | Cancel button per row, Cancel All button, modify action |

### Signing Considerations

Cancel and modify likely use different EIP-712 type structures than order placement. The `HyperliquidSigner` must support:
- Order placement type (proven in F5)
- Cancel type (order ID + nonce)
- Modify type (order ID + new parameters + nonce)

Each type must have its own domain-compatible type hash.

---

## Out of Scope

- Batch modify
- Order history (only current open orders)
- Undo/rollback after cancel

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Single order cancel is signed correctly and accepted by Hyperliquid
- [ ] "Cancel All" cancels all open orders
- [ ] Order modification (price/size change) is signed correctly and accepted
- [ ] Confirmation dialog shown before cancel and cancel-all actions
- [ ] Orders table updates after cancel/modify without manual refresh
- [ ] Cancelling an already-filled or already-cancelled order shows a clear message
- [ ] Signing errors produce clear, distinguishable error messages

---

## Related Features

- **F2** — Orders table in the dashboard is the UI surface for management actions
- **F5** — Order placement must work before cancel/modify can be tested
- **F7** — Order update events from WebSocket will also update the orders table
