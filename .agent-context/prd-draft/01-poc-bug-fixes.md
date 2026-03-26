# PRD: POC Bug Fixes

**Status:** Draft  
**Priority:** 1 (small — do before or in parallel with Epic 2)  
**Date:** 2026-03-26

---

## Summary

Fix correctness debt identified in POC reviews before building new features.

## Known Issues

### 1. Cancel All Orders Bug

- **Location:** `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts`
- **Problem:** The UI clears `this.orders = []` before reading the asset from the array, so `cancelAllOrders()` falls back to `"BTC"` instead of the user's actual asset.
- **Impact:** On any non-BTC asset, the wrong cancellation request is sent. This is a real functional bug in a trading system.
- **Fix:** Read the asset before clearing the optimistic state.

### 2. Misleading 24h Market Data Labels

- **Location:** `MarketDataStreamService.cs`, `price-ticker.component.ts`
- **Problem:** High/low are seeded from the current mid price at startup, volume is seeded once from REST and then mutated in-process. All rendered as "24h High", "24h Low", "24h Volume" — but they reset on restart and drift from actual exchange 24h values.
- **Impact:** Misleading market context for a trading product.
- **Fix:** Either relabel honestly (e.g. "Session High") or periodically refresh from the exchange REST API.

### 3. Debug Endpoints on Public Surface

- **Location:** `OrdersController.cs` — `/debug/mids`, `/debug/meta`, `/debug/clearinghouse`
- **Problem:** Raw exchange state exposed on the main API surface.
- **Fix:** Move behind a `#if DEBUG` guard or separate controller with authorization.

## Out of Scope

- Multi-tenant migration
- New features
- Dependency upgrades (AutoMapper advisory — track separately)

## Acceptance Criteria

- [ ] Cancel all orders sends the correct asset for non-BTC positions
- [ ] Market data labels accurately reflect what the data represents
- [ ] Debug endpoints are not accessible in release builds

---

## Notes

<!-- Flesh out further as needed -->
