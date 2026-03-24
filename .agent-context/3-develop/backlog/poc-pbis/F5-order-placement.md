# PBI Specification: F5 — Order Placement

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 4  
**Risk Level:** **High**  
**Depends On:** F1, F2, F3

---

## Summary

Place market and limit orders on Hyperliquid testnet from the Angular UI, proving that EIP-712 typed data signing works correctly in .NET via Nethereum.

### User Story

> As a **developer**, I want to **place orders on testnet** so that **I can prove the EIP-712 signing and order submission flow works end-to-end**.

### Business Value

This is the **single highest-risk item** in the POC. The entire trading platform depends on the ability to sign and submit orders from .NET. If Nethereum's EIP-712 implementation is not compatible with Hyperliquid, this is where the blocker will be discovered.

---

## Requirements

### Functional Requirements

- [ ] Place a market order (buy/sell BTC-PERP, specified size)
- [ ] Place a limit order (buy/sell BTC-PERP, specified price and size)
- [ ] EIP-712 typed data signature generated correctly in .NET using Nethereum
- [ ] Nonce management: monotonically increasing, no collisions under rapid submission
- [ ] Angular UI with order entry form (side, type, price, size)
- [ ] Success/error feedback shown in UI after submission
- [ ] New order appears in the open orders table (F2 dashboard)

### Non-Functional Requirements

- [ ] Order round-trip latency (submit → confirmed) measured and logged
- [ ] Signing errors clearly distinguished from other API errors

---

## User Flow

### Happy Path — Market Order

1. Developer navigates to Order Entry tab
2. Selects "Buy" or "Sell"
3. Selects "Market" order type
4. Enters size (e.g. 0.001 BTC)
5. Clicks "Submit Order"
6. UI shows success confirmation with order details
7. Dashboard positions table updates with new position

### Happy Path — Limit Order

1. Developer navigates to Order Entry tab
2. Selects "Buy" or "Sell"
3. Selects "Limit" order type
4. Enters price and size
5. Clicks "Submit Order"
6. UI shows success confirmation
7. Dashboard orders table shows the new open order

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Invalid signature rejected by Hyperliquid | Clear error message identifying signature rejection; signing parameters logged |
| Insufficient margin | Error from exchange surfaced in UI with "insufficient margin" detail |
| Invalid size (too small / too large) | Exchange validation error surfaced in UI |
| Nonce collision | Backend detects and retries with incremented nonce (or surfaces error) |
| Network error during submission | Error surfaced in UI; order status unknown — must check dashboard |
| Price field empty for limit order | Frontend validation prevents submission |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders` | Places a new order (market or limit) |

### Request Body

```json
{
  "asset": "BTC-PERP",
  "side": "buy",
  "orderType": "limit",
  "price": 65000.00,
  "size": 0.001
}
```

### Response

```json
{
  "success": true,
  "orderId": "0x...",
  "status": "open",
  "detail": null
}
```

### EIP-712 Signing Flow

1. Construct the order action payload per Hyperliquid's expected schema
2. Build EIP-712 typed data structure (domain separator, type definitions, message)
3. Sign using `Eip712TypedDataSigner` from Nethereum with the configured private key
4. Attach signature (v, r, s) to the API request
5. Submit to Hyperliquid REST API

### Key Validation Points

| Concern | What to Verify |
|---------|---------------|
| Domain separator | Matches Hyperliquid's chain ID and verifying contract |
| Primary type | Correct type name and field list for order action |
| Type hashes | Match what Hyperliquid's backend computes |
| Nonce | Monotonically increasing; timestamp-based or counter-based |
| Signature encoding | v, r, s in the format Hyperliquid expects (hex string vs bytes) |

### Key Components

| Component | Action |
|-----------|--------|
| `OrderController` | POST endpoint for order placement |
| `HyperliquidSigner` | Constructs and signs EIP-712 typed data |
| `HyperliquidRestClient` | Submits signed order to Hyperliquid API |
| `hyperliquid-api.service.ts` | Angular service calling order endpoint |
| Order Entry feature component | Form with side, type, price, size fields and submit button |

---

## Out of Scope

- Stop-loss or take-profit order types
- Batch order submission
- Order persistence (in-memory only)
- Order validation beyond exchange response (no local risk engine)

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Market order (buy and sell) accepted by Hyperliquid testnet
- [ ] Limit order (buy and sell) accepted by Hyperliquid testnet
- [ ] EIP-712 signature is generated correctly and accepted on first valid attempt
- [ ] Nonces are monotonically increasing with no collisions
- [ ] Order entry form validates required fields before submission
- [ ] Success/error feedback displayed in UI after each submission
- [ ] Order round-trip latency logged
- [ ] Signing errors produce a clear, distinguishable error message
- [ ] New orders appear in the dashboard orders/positions tables (F2)

---

## Related Features

- **F1** — Private key and connectivity must be established
- **F2** — Dashboard displays orders placed here
- **F3** — REST client proven before adding write operations
- **F6** — Order management (cancel/modify) extends the signing proven here
