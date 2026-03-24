# PBI Specification: F8 — Error Handling & Resilience

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 8 (Hardening)  
**Risk Level:** Low  
**Depends On:** F1–F7

---

## Summary

Harden error handling, resilience, and logging across all POC features. This is a cross-cutting pass that ensures API errors, signing failures, rate limits, and WebSocket disconnects are handled consistently and surfaced to the UI.

### User Story

> As a **developer**, I want to **understand how the integration behaves under failure conditions** so that **I can document edge cases and build confidence that the production system can be made robust**.

### Business Value

Validates that error paths are observable and recoverable. Findings from this hardening pass inform the production error-handling strategy. Without this, the POC only proves happy paths.

---

## Requirements

### Functional Requirements

- [ ] API errors (4xx, 5xx) are caught and surfaced to the UI with meaningful messages
- [ ] Invalid signature errors are clearly identified and distinguished from other errors
- [ ] Rate limit responses (429) trigger exponential backoff and retry
- [ ] WebSocket disconnects trigger automatic reconnection with exponential backoff
- [ ] After WebSocket reconnect, open orders and positions are resynced via REST
- [ ] All errors logged with structured logging (Serilog)

### Non-Functional Requirements

- [ ] Structured log fields include: timestamp, correlation ID, error type, endpoint, detail
- [ ] Rate-limit backoff: initial 1s, max 60s, exponential increase
- [ ] No unhandled exceptions crash the backend process

---

## User Flow

### Scenario — API Error

1. Developer submits an order with invalid parameters
2. Hyperliquid returns a 4xx error
3. Backend catches and wraps the error with a meaningful message
4. Angular UI displays the error in a toast/banner
5. Serilog logs the error with structured fields

### Scenario — Signing Error

1. Developer has a misconfigured signing setup (e.g. wrong chain ID during debugging)
2. Order submission returns a signature validation failure
3. Backend identifies this as a signing error specifically
4. UI displays "Signature rejected — check signing configuration"
5. Log includes the signing parameters for debugging

### Scenario — Rate Limit

1. Developer submits several rapid requests
2. Hyperliquid returns 429
3. Backend queues the request, waits backoff interval, retries
4. If retries exhausted, error surfaced to UI
5. Log records rate-limit hit and retry attempts

### Scenario — WebSocket Reconnect with State Resync

1. Hyperliquid WebSocket drops (network interruption)
2. Backend detects disconnect, begins reconnection with backoff
3. On successful reconnect, resubscribes to streams
4. Backend re-fetches open orders and positions via REST
5. Dashboard updates with resynced state
6. Activity feed shows reconnection event

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Repeated 429 responses | Backoff increases to max (60s); if all retries exhausted, error surfaced to UI |
| WebSocket fails to reconnect after max backoff | Status stays "Disconnected"; logged as critical; user sees persistent error |
| Hyperliquid API returns unexpected status code | Logged with full response body; generic error shown in UI |
| Backend process encounters unhandled exception | Global exception handler catches it; logged; process does not crash |

---

## Technical Considerations

### Error Categories

| Category | Source | UI Display | Log Level |
|----------|--------|-----------|-----------|
| Validation error | Hyperliquid 4xx | Specific message (e.g. "insufficient margin") | Warning |
| Signing error | Hyperliquid signature rejection | "Signature rejected — check signing config" | Error |
| Rate limit | Hyperliquid 429 | "Rate limited — retrying..." (then success or failure) | Warning |
| Network error | HTTP timeout, connection refused | "Cannot reach exchange — retrying..." | Error |
| WebSocket disconnect | Connection drop | Status badge changes; reconnection automatic | Warning |
| Internal error | Unhandled exception | "Internal error — check logs" | Error |

### Key Components

| Component | Action |
|-----------|--------|
| Global exception middleware | Catches unhandled exceptions; returns structured error response |
| `HyperliquidRestClient` | Wraps HTTP calls with error classification, retry for 429, timeout handling |
| `HyperliquidWebSocketClient` | Reconnection with backoff; state resync trigger after reconnect |
| Angular error interceptor | HTTP interceptor that catches error responses and displays toasts/banners |
| Serilog configuration | Structured logging to console (and file if useful); enriched with correlation fields |

### Structured Log Example

```json
{
  "Timestamp": "2026-03-25T14:30:00Z",
  "Level": "Error",
  "MessageTemplate": "Hyperliquid API error",
  "Properties": {
    "Endpoint": "/exchange",
    "StatusCode": 400,
    "ErrorType": "SignatureRejected",
    "Detail": "Invalid signature",
    "CorrelationId": "abc-123"
  }
}
```

### State Resync After Reconnect

When a WebSocket connection is restored:
1. Re-fetch open orders via `GET /api/account/orders`
2. Re-fetch positions via `GET /api/account/positions`
3. Compare with last known state
4. Push any differences to Angular via SignalR
5. Log discrepancies for debugging

---

## Out of Scope

- Alerting (email, SMS, Slack)
- Circuit breaker pattern (overkill for POC)
- Distributed tracing
- Log aggregation / centralised logging
- Automated recovery testing

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] API errors (4xx/5xx) are caught and displayed in the UI with meaningful messages
- [ ] Signing errors are specifically identified and distinguishable from other errors
- [ ] 429 responses trigger backoff and retry; retries are logged
- [ ] WebSocket disconnects trigger automatic reconnection with exponential backoff
- [ ] After WebSocket reconnect, orders and positions are resynced via REST
- [ ] All errors are logged with Serilog using structured fields
- [ ] No unhandled exceptions crash the backend process
- [ ] Error banner/toast component works across all UI screens

---

## Related Features

- Applies to all features (F1–F7) as a cross-cutting hardening pass
- Specific signing error handling builds on F5 findings
- WebSocket reconnection builds on F4 and F7 connection management
