# PBI Specification: F5 — Order Placement

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 4
**Risk Level:** High
**Depends On:** F1, F3

---

## Summary

Place market and limit orders on Hyperliquid testnet via the Angular UI, with EIP-712 typed data signing handled in .NET. This is the single highest-risk item in the POC — if Nethereum's EIP-712 implementation is not compatible with Hyperliquid's expected signature format, this is where the blocker will be discovered.

### User Story

> As a **developer**, I want to **place orders on Hyperliquid testnet** so that **I can prove the EIP-712 signing and order submission flow works end-to-end from .NET**.

### Business Value

The entire trading platform depends on the ability to sign and submit orders from .NET. Proving EIP-712 signing compatibility with Hyperliquid is the critical risk to retire in this POC.

---

## Problem Statement

The entire trading platform depends on the ability to sign and submit orders from .NET using Hyperliquid's wallet-based EIP-712 typed data signing. This is the single highest-risk item in the POC. If Nethereum's EIP-712 implementation is not compatible with Hyperliquid's expected signature format, this is where the blocker will be discovered.

## Requirements

### Functional Requirements

1. Place a **market order** (buy/sell BTC-PERP, specified size) via the Angular UI
2. Place a **limit order** (buy/sell BTC-PERP, specified price and size) via the Angular UI
3. EIP-712 typed data signature generated correctly in .NET using Nethereum
4. Nonce management using UTC timestamp in milliseconds (monotonically increasing, no collisions)
5. Angular UI order entry form with: side toggle (Buy/Sell), order type selector (Market/Limit), price field (limit only), and size field
6. Limit order price field pre-populated with current mid price from market data (F3)
7. Confirmation dialog shown before order submission, displaying order summary (side, type, asset, price, size)
8. Success/error feedback shown in UI after submission — display full Hyperliquid error payload on failure
9. New order appears in the open orders table (F2 dashboard) after successful placement
10. Standalone signing diagnostic endpoint (`POST /api/orders/test-sign`) that signs a dummy payload and returns signature details without submitting to the exchange — used to verify EIP-712 compatibility independently

### Non-Functional Requirements

- Order round-trip latency (submit → confirmed) measured and logged via Serilog structured logging (submit time, response time, delta ms)
- Signing errors clearly distinguished from other API errors in both logs and UI error display

## User Flow

### Happy Path

1. Developer navigates to Order Entry tab in the Angular UI
2. Developer selects side (Buy/Sell) and order type (Market/Limit)
3. For limit orders, price field is pre-populated with current mid price from F3; developer adjusts if needed
4. Developer enters size
5. Developer clicks Submit — confirmation dialog shows order summary (side, type, asset, price, size)
6. Developer confirms — order is signed with EIP-712 and submitted to Hyperliquid testnet
7. Success message displayed with order details; order appears in F2 dashboard orders table

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid returns error (e.g. insufficient margin) | Full error payload displayed in UI |
| EIP-712 signature rejected | UI clearly identifies as signature rejection; backend logs signing parameters |
| Network error during submission | Error message displayed in UI |
| Nonce collision (sub-millisecond submissions) | Each order receives unique monotonically increasing nonce |

---

## Technical Considerations

### API Endpoints (if relevant)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders` | Place a new order (market or limit) |
| POST | `/api/orders/test-sign` | Diagnostic: sign a dummy payload and return signature details |

**POST `/api/orders` Request:**
```json
{
  "asset": "BTC-PERP",
  "side": "buy",
  "orderType": "limit",
  "price": 65000.00,
  "size": 0.001
}
```

**POST `/api/orders` Response (success):**
```json
{
  "success": true,
  "orderId": "0x...",
  "status": "open",
  "detail": null
}
```

**POST `/api/orders` Response (error):**
```json
{
  "success": false,
  "orderId": null,
  "status": "rejected",
  "detail": "{ raw Hyperliquid error payload }"
}
```

**POST `/api/orders/test-sign` Response:**
```json
{
  "domainSeparator": "0x...",
  "typeHash": "0x...",
  "messageHash": "0x...",
  "signature": {
    "v": 27,
    "r": "0x...",
    "s": "0x..."
  }
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
| Nonce | Monotonically increasing; timestamp-based (UTC ms) |
| Signature encoding | v, r, s in the format Hyperliquid expects (hex string vs bytes) |

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Asset selection | Hard-coded to BTC-PERP | POC scope — single asset simplifies testing |
| Size validation | No frontend min/max constraints | Let Hyperliquid validate; arbitrary sizes allowed |
| Limit price default | Pre-populate with mid price | Faster order entry; developer adjusts from there |
| Confirmation step | Confirmation dialog before submit | Prevents accidental submissions despite POC context |
| Time-in-force | GTC only (default) | No TIF selector in UI; all limit orders are Good Till Cancel |
| Reduce-only | Not supported | Out of scope for POC |
| Nonce strategy | Timestamp-based (UTC milliseconds) | Simple, monotonic, standard for Hyperliquid |
| Latency measurement | Structured logging only | No UI display; Serilog structured fields for analysis |
| Error display | Full Hyperliquid error payload | POC is developer-facing; raw errors aid debugging |
| Signing diagnostic | Dedicated test-sign endpoint | De-risks highest-risk item independently from order flow |

## Out of Scope

- Stop-loss or take-profit order types
- Batch order submission
- Order persistence (in-memory only for POC)
- Order validation beyond exchange response (no local risk engine in POC)
- Reduce-only orders
- Time-in-force options (IOC, ALO) — GTC only

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** the developer is on the Order Entry tab and selects Buy, Market, and enters a valid size, **When** they click Submit and confirm in the confirmation dialog, **Then** the order is submitted to Hyperliquid testnet and a success message with order details is displayed
- [ ] **Given** the developer is on the Order Entry tab and selects Sell, Limit, and enters a valid price and size, **When** they click Submit and confirm, **Then** the order is submitted and appears in the F2 open orders table
- [ ] **Given** the developer selects Limit order type, **When** the price field is rendered, **Then** it is pre-populated with the current mid price from F3 market data
- [ ] **Given** the developer clicks Submit, **When** the confirmation dialog appears, **Then** it displays side, type, asset (BTC-PERP), price (if limit), and size for review before final confirmation
- [ ] **Given** the developer submits an order and Hyperliquid returns an error (e.g., insufficient margin, invalid size), **When** the error response is received, **Then** the full error payload from Hyperliquid is displayed in the UI
- [ ] **Given** the developer submits an order and the EIP-712 signature is rejected by Hyperliquid, **When** the error is received, **Then** the UI clearly identifies it as a signature rejection and the backend logs the signing parameters
- [ ] **Given** two orders are submitted in rapid succession (sub-millisecond), **When** nonces are generated, **Then** each order receives a unique monotonically increasing timestamp-based nonce with no collisions
- [ ] **Given** the developer calls `POST /api/orders/test-sign` with a dummy payload, **When** the signing completes, **Then** the response includes domain separator, type hash, message hash, and the (v, r, s) signature components — without sending anything to Hyperliquid
- [ ] **Given** an order is submitted successfully, **When** the backend logs the transaction, **Then** structured log fields include submit timestamp, response timestamp, and round-trip delta in milliseconds

### Release Notes Information

- **Heading**: Order Placement on Hyperliquid Testnet
- **Release note type**: Feature
- **Release Note Summary**: Place market and limit orders on Hyperliquid testnet via the Angular UI, with EIP-712 typed data signing handled in .NET. Includes signing diagnostics and full error reporting.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Wallet configuration and `HyperliquidSigner` are prerequisites for signing
- **F3** — Mid price used to pre-populate the limit order price field
- **F2** — New orders appear in the dashboard orders table
- **F6** — Order management (cancel/modify) builds on the signing proven here
- **F7** — Fill events from the user event stream confirm order execution
