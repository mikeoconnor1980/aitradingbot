# PBI Specification: F8 — Error Handling & Resilience

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 8 (Hardening)
**Risk Level:** Low
**Depends On:** F1–F7

---

## Summary

A cross-cutting hardening pass that standardises error handling, retry logic, and resilience patterns across all POC features. This PBI does not re-implement WebSocket reconnection (already defined in F4/F7) but adds REST resync after reconnection, rate-limit retry logic, structured logging consistency, and a unified UI error notification component.

### User Story

> As a **developer**, I want **consistent error handling and resilience across all POC features** so that **I can observe failure behaviour, document edge cases, and build confidence that the production system can be made robust**.

### Business Value

Without observable and recoverable error paths, the POC cannot inform production design decisions. This hardening pass ensures all failure scenarios are logged, surfaced, and handled consistently.

---

## Problem Statement

The POC features (F1–F7) each define their own error handling in isolation. This pass standardises error categorisation, logging format, retry behaviour, and UI notification patterns across all features. It also adds capabilities not covered by individual features: REST state resync after WebSocket reconnection, rate-limit retry logic, and global exception handling.

## Requirements

### Functional Requirements

1. API errors (4xx, 5xx) from Hyperliquid are caught and surfaced to the UI with meaningful, categorised messages
2. Invalid signature errors are clearly identified and distinguished from other API errors
3. Rate limit responses (429) trigger exponential backoff and automatic retry before surfacing failure to the user
4. WebSocket disconnects trigger automatic reconnection with exponential backoff (reconnection logic itself is defined in F4/F7; this PBI adds the REST resync step below)
5. After WebSocket reconnect, open orders and positions are resynced via REST to ensure UI state is accurate (new behaviour not covered by F4/F7)
6. All errors are logged with structured logging including: timestamp, correlation ID, error type, endpoint, and detail
7. A reusable error notification component (toast/banner) displays errors consistently across all UI screens

### Non-Functional Requirements

- Rate-limit backoff: initial 1s, max 60s, exponential increase
- No unhandled exceptions crash the backend process
- WebSocket reconnection parameters: initial 1s, 60s max, 20 retry cap (consistent with F4/F7)

## Technical Considerations

### Error Categories (Behavioural)

| Category | Source | UI Display | Log Level |
|----------|--------|-----------|-----------|
| Validation error | Hyperliquid 4xx | Specific message (e.g. "insufficient margin") | Warning |
| Signing error | Hyperliquid signature rejection | "Signature rejected — check signing config" | Error |
| Rate limit | Hyperliquid 429 | "Rate limited — retrying..." (then success or failure) | Warning |
| Network error | HTTP timeout, connection refused | "Cannot reach exchange — retrying..." | Error |
| WebSocket disconnect | Connection drop | Status badge changes; reconnection automatic | Warning |
| Internal error | Unhandled exception | "Internal error — check logs" | Error |

### Integration Events (if relevant)

WebSocket reconnection should trigger a state resync that pushes updated orders/positions to the UI in real time.

## Out of Scope

- Alerting (email, SMS, Slack)
- Circuit breaker pattern
- Distributed tracing
- Log aggregation / centralised logging
- Automated recovery testing
- Health check endpoints beyond F1's existing `GET /api/health`

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

### API Error Handling

- [ ] **Given** a request to Hyperliquid returns a 4xx error, **When** the backend processes the response, **Then** a meaningful error message (e.g. "insufficient margin") is displayed in the UI and logged with structured fields
- [ ] **Given** a request to Hyperliquid returns a 5xx error, **When** the backend processes the response, **Then** a generic exchange error message is displayed in the UI and logged with structured fields
- [ ] **Given** a request to Hyperliquid returns an unexpected status code, **When** the backend processes the response, **Then** the full response body is logged and a generic error is shown in the UI

### Signing Error Handling

- [ ] **Given** the signing configuration is invalid (e.g. wrong chain ID), **When** an order is submitted, **Then** the UI displays "Signature rejected — check signing configuration" and the error is logged separately from other API errors

### Rate Limit Handling

- [ ] **Given** Hyperliquid returns a 429 response, **When** the backend receives the response, **Then** the request is retried with exponential backoff (starting at 1s, max 60s) and the retry attempts are logged
- [ ] **Given** Hyperliquid returns repeated 429 responses, **When** all retry attempts are exhausted, **Then** the failure is surfaced to the UI with a rate-limit-specific message

### WebSocket Resilience

- [ ] **Given** the WebSocket connection to Hyperliquid drops, **When** the disconnect is detected, **Then** automatic reconnection begins with exponential backoff (1s initial, 60s max, 20 retry cap — as defined in F4/F7) and the UI connection status updates to reflect the disconnected state
- [ ] **Given** a WebSocket reconnection succeeds, **When** the connection is re-established, **Then** all stream subscriptions are restored (per F4/F7), open orders and positions are resynced via REST (new in F8), and the dashboard reflects the current state
- [ ] **Given** a WebSocket reconnection fails after 20 retry attempts, **When** no further retries will be attempted, **Then** the connection status remains "Disconnected", the failure is logged at critical level, and the user sees a persistent error indicator

### Structured Logging

- [ ] **Given** any error occurs (API, signing, rate limit, network, WebSocket, or unhandled exception), **When** the error is logged, **Then** the log entry includes timestamp, correlation ID, error type, endpoint (if applicable), and detail as structured fields

### Global Error Handling

- [ ] **Given** an unhandled exception occurs in the backend, **When** the exception propagates, **Then** a global handler catches it, logs it, returns a structured error response, and the process does not crash

### UI Error Display

- [ ] **Given** any error is returned from the backend, **When** the Angular UI receives the error response, **Then** a toast or banner notification displays the error message consistently regardless of which screen the user is on

### Release Notes Information

- **Heading**: Error Handling & Resilience Hardening
- **Release note type**: Enhancement
- **Release Note Summary**: Added consistent error handling, retry logic for rate limits, automatic WebSocket reconnection with state resync, structured logging, and a unified UI error notification component across all POC features.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F4** — WebSocket reconnection logic (market data) is defined in F4; this PBI standardises and extends it
- **F7** — WebSocket reconnection logic (user events) is defined in F7; this PBI adds REST resync after reconnection
- **F5** — Signing error handling and latency logging are defined in F5; this PBI standardises the error categorisation
