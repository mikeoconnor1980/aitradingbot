---
applyTo: ".agent-context/3-develop/build/changes/20260326-error-handling-resilience-changes.md"
currentAgent: "Implementation Reviewer"
agentStartedAt: "2026-03-26T15:03:03Z"
status: "reviewing"
lastUpdated: "2026-03-26T15:03:03Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F8 — Error Handling & Resilience

## Overview

Cross-cutting hardening pass that standardises error handling, retry logic, and resilience patterns across all POC features — adding typed exceptions, enhanced error envelopes with correlation IDs, HTTP retry policies for rate limits, WebSocket REST resync after reconnection, structured logging consistency, and a unified Angular error notification component.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Implementation Phase:** 8 (Hardening)
**Risk Level:** Low
**Depends On:** F1–F7

### User Story

> As a **developer**, I want **consistent error handling and resilience across all POC features** so that **I can observe failure behaviour, document edge cases, and build confidence that the production system can be made robust**.

### Acceptance Criteria

#### API Error Handling

- [ ] Given a request to Hyperliquid returns a 4xx error, When the backend processes the response, Then a meaningful error message (e.g. "insufficient margin") is displayed in the UI and logged with structured fields
- [ ] Given a request to Hyperliquid returns a 5xx error, When the backend processes the response, Then a generic exchange error message is displayed in the UI and logged with structured fields
- [ ] Given a request to Hyperliquid returns an unexpected status code, When the backend processes the response, Then the full response body is logged and a generic error is shown in the UI

#### Signing Error Handling

- [ ] Given the signing configuration is invalid (e.g. wrong chain ID), When an order is submitted, Then the UI displays "Signature rejected — check signing configuration" and the error is logged separately from other API errors

#### Rate Limit Handling

- [ ] Given Hyperliquid returns a 429 response, When the backend receives the response, Then the request is retried with exponential backoff (starting at 1s, max 60s) and the retry attempts are logged
- [ ] Given Hyperliquid returns repeated 429 responses, When all retry attempts are exhausted, Then the failure is surfaced to the UI with a rate-limit-specific message

#### WebSocket Resilience

- [ ] Given the WebSocket connection to Hyperliquid drops, When the disconnect is detected, Then automatic reconnection begins with exponential backoff (1s initial, 60s max, 20 retry cap) and the UI connection status updates to reflect the disconnected state
- [ ] Given a WebSocket reconnection succeeds, When the connection is re-established, Then all stream subscriptions are restored, open orders and positions are resynced via REST, and the dashboard reflects the current state
- [ ] Given a WebSocket reconnection fails after 20 retry attempts, When no further retries will be attempted, Then the connection status remains "Disconnected", the failure is logged at critical level, and the user sees a persistent error indicator

#### Structured Logging

- [ ] Given any error occurs (API, signing, rate limit, network, WebSocket, or unhandled exception), When the error is logged, Then the log entry includes timestamp, correlation ID, error type, endpoint (if applicable), and detail as structured fields

#### Global Error Handling

- [ ] Given an unhandled exception occurs in the backend, When the exception propagates, Then a global handler catches it, logs it, returns a structured error response, and the process does not crash

#### UI Error Display

- [ ] Given any error is returned from the backend, When the Angular UI receives the error response, Then a toast or banner notification displays the error message consistently regardless of which screen the user is on

## Objectives

- Standardise all API error responses to use the enhanced `Envelope` shape with `ErrorCode` and `CorrelationId`
- Add typed exception hierarchy for Hyperliquid-specific errors (rate limit, signing, API errors)
- Add HTTP retry pipeline with exponential backoff for 429 and transient errors
- Trigger REST state resync (orders + positions) after WebSocket reconnection
- Emit `Reconnecting` state from backend WebSocket layer
- Add correlation ID middleware that enriches all log entries
- Refactor `AccountController` to use global exception filter (remove shadow try/catch)
- Create unified Angular `NotificationService` and HTTP error interceptor
- Eliminate duplicated `_formatErrorPayload` across frontend components

### Discovery References

**Key findings from codebase analysis:**

- `HttpGlobalExceptionFilter` handles exceptions globally but maps all `HttpRequestException` to 503 (no 429 differentiation)
- `Envelope` has only `ErrorMessage` + `Timestamp` — no error code or correlation ID
- `AccountController` bypasses global filter with per-endpoint try/catch returning anonymous `{ error: "..." }` objects
- `HyperliquidRestClient` throws `HttpRequestException` with status code but no typed exceptions — no retry logic
- `MarketDataStreamService` has exponential backoff reconnect but `SeedStatsFromRestAsync` only runs at startup, not after reconnect
- `WebSocketConnectionState.Reconnecting` enum exists but is never emitted by the backend
- `HyperliquidOrderService` detects signing rejection via fragile string matching (`ex.Message.Contains("signature")`)
- No Polly or `Microsoft.Extensions.Http.Resilience` package referenced anywhere
- Frontend has 3 inconsistent error display mechanisms: `MatSnackBar`, inline error banners, inline error text
- `_formatErrorPayload` method is copy-pasted identically in `DashboardComponent` and `OrderEntryComponent`
- No Angular HTTP interceptor — `provideHttpClient()` has no interceptors registered
- No global Angular `ErrorHandler` override

### Project Patterns

- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Global exception-to-HTTP-status mapping pattern
- `src/TradingApp.Api/Infrastructure/Envelope.cs` — Error response envelope shape
- `src/TradingApp.Api/Program.cs` — DI composition root, HttpClient registration, filter registration
- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — REST client, error wrapping pattern
- `src/TradingApp.Api/Services/MarketDataStreamService.cs` — BackgroundService with exponential backoff reconnect
- `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Signing rejection detection via string match
- `src/TradingApp.Api/Controllers/AccountController.cs` — Controller with shadow error handling (to be refactored)
- `src/TradingApp.Application/Abstractions/Exceptions/DomainException.cs` — Typed domain exception pattern
- `tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — HTTP message handler test helper
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — WebApplicationFactory integration test pattern
- `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — Controller integration test pattern
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Error banner + snackbar pattern
- `frontend/trading-ui/src/app/core/services/signalr.service.ts` — SignalR connection lifecycle pattern
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — REST client service pattern

### [x] Phase 1: Backend Error Infrastructure

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create typed exception hierarchy for Hyperliquid errors
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-11-create-typed-exception-hierarchy

- [x] Task 1.2: Enhance Envelope with ErrorCode and CorrelationId
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-12-enhance-envelope-with-errorcode-and-correlationid

- [x] Task 1.3: Add correlation ID middleware
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-13-add-correlation-id-middleware

- [x] Task 1.4: Update HttpGlobalExceptionFilter with new exception mappings
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-14-update-httpglobalexceptionfilter-with-new-exception-mappings

- [x] Task 1.5: Refactor AccountController to use global exception filter
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-15-refactor-accountcontroller-to-use-global-exception-filter

- [x] Task 1.6: Register middleware and update tests
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-01-details.md#task-16-register-middleware-and-update-tests

### [x] Phase 2: Backend HTTP Resilience

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Add Microsoft.Extensions.Http.Resilience package and configure retry pipeline
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-02-details.md#task-21-add-http-resilience-package-and-configure-retry-pipeline

- [x] Task 2.2: Enhance HyperliquidRestClient with typed exception throwing
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-02-details.md#task-22-enhance-hyperliquidrestclient-with-typed-exception-throwing

- [x] Task 2.3: Update HyperliquidOrderService signing error detection
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-02-details.md#task-23-update-hyperliquidorderservice-signing-error-detection

- [x] Task 2.4: Add tests for retry behaviour and typed exceptions
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-02-details.md#task-24-add-tests-for-retry-behaviour-and-typed-exceptions

### [x] Phase 3: Backend WebSocket Resilience

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Add REST state resync after WebSocket reconnection
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-03-details.md#task-31-add-rest-state-resync-after-websocket-reconnection

- [x] Task 3.2: Emit Reconnecting state from backend WebSocket layer
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-03-details.md#task-32-emit-reconnecting-state-from-backend-websocket-layer

- [x] Task 3.3: Add tests for reconnection with REST resync
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-03-details.md#task-33-add-tests-for-reconnection-with-rest-resync

### [x] Phase 4: Frontend Error Infrastructure & Component Refactoring

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Create NotificationService wrapping MatSnackBar
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-04-details.md#task-41-create-notificationservice-wrapping-matsnackbar

- [x] Task 4.2: Create ErrorDto model and shared error utility
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-04-details.md#task-42-create-errordto-model-and-shared-error-utility

- [x] Task 4.3: Create HTTP error interceptor
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-04-details.md#task-43-create-http-error-interceptor

- [x] Task 4.4: Refactor components to use NotificationService
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-04-details.md#task-44-refactor-components-to-use-notificationservice

- [x] Task 4.5: Fix inconsistent error styling and run build/lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-error-handling-resilience-phase-04-details.md#task-45-fix-inconsistent-error-styling-and-run-buildlint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend Error Infrastructure | Medium | Low |
| Phase 2: Backend HTTP Resilience | Medium | Medium |
| Phase 3: Backend WebSocket Resilience | Medium | Low |
| Phase 4: Frontend Error Infrastructure & Component Refactoring | Medium | Low |
| **Total** | **Medium** | **Low–Medium** |

### Scoping Notes

- Circuit breaker pattern is explicitly out of scope per PBI
- WebSocket reconnection logic itself already exists in F4/F7 — this PBI extends it with REST resync
- No new database entities or persistence (RiskEvent/AuditLog are out of scope for this PBI)
- Rate-limit retry applies only to REST calls to Hyperliquid, not to WebSocket
- Global Angular ErrorHandler override is out of scope — the HTTP interceptor covers the REST path which is all that's needed for the POC
- Health check endpoints remain unchanged (per F1's existing `GET /api/health`)

## Dependencies

- `Microsoft.Extensions.Http.Resilience` NuGet package (brings Polly v8 as transitive dependency)
- Existing `@angular/material` MatSnackBar (already installed)
- All F1–F7 features completed

## Success Criteria

- All API error responses use the enhanced `Envelope` shape with `ErrorCode`, `CorrelationId`, `ErrorMessage`, and `Timestamp`
- No controllers have shadow try/catch blocks — all errors flow through `HttpGlobalExceptionFilter`
- 429 responses from Hyperliquid are retried automatically with exponential backoff before surfacing to UI
- WebSocket reconnection triggers REST resync of orders and positions
- All log entries for errors include correlation ID, error type, and structured fields
- Unified toast notification appears on any screen when an API error occurs
- `_formatErrorPayload` exists in exactly one place (shared utility)
- All existing tests pass + new tests cover all error categories
- `dotnet build` and `ng build` + `ng lint` pass

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-03-26T13:25:59Z | 2026-03-26T13:59:07Z |
| Plan Reviewer | plan-reviewed | 2026-03-26T14:01:07Z | 2026-03-26T14:07:50Z |
| Plan Implementer | implemented | 2026-03-26T14:10:42Z | 2026-03-26T15:00:44Z |
| Implementation Reviewer | reviewing | 2026-03-26T15:03:03Z | |
