<!-- markdownlint-disable-file -->
# Release Changes: F8 — Error Handling & Resilience

**Related Plan**: 20260326-error-handling-resilience-plan.instructions.md
**Implementation Date**: 2026-03-26

## Summary

Cross-cutting hardening pass standardising error handling, retry logic, and resilience patterns across all POC features — typed exceptions, enhanced error envelopes with correlation IDs, HTTP retry policies for rate limits, WebSocket REST resync after reconnection, structured logging consistency, and a unified Angular error notification component.

## Changes

### Added

<!-- Phase 1: Backend Error Infrastructure -->
- src/TradingApp.Application/Abstractions/Exceptions/HyperliquidApiException.cs: Base exception for all Hyperliquid API errors with ExchangeStatusCode and ErrorCategory properties
- src/TradingApp.Application/Abstractions/Exceptions/RateLimitException.cs: Sealed exception for 429 rate-limit errors with optional RetryAfterSeconds
- src/TradingApp.Application/Abstractions/Exceptions/SigningException.cs: Sealed exception for EIP-712 signing failures
- src/TradingApp.Api/Infrastructure/CorrelationIdMiddleware.cs: Middleware that propagates or generates X-Correlation-ID header and enriches log scope

<!-- Phase 2: Backend HTTP Resilience -->
(no new files)

<!-- Phase 3: Backend WebSocket Resilience -->
(no new files)

<!-- Phase 4: Frontend Error Infrastructure & Component Refactoring -->
- frontend/trading-ui/src/app/core/services/notification.service.ts: Centralized notification service wrapping MatSnackBar with error/success/warning/info severity methods
- frontend/trading-ui/src/app/core/models/error.model.ts: ErrorDto interface matching backend Envelope shape
- frontend/trading-ui/src/app/core/utils/error-utils.ts: formatErrorPayload and extractErrorCode shared utility functions
- frontend/trading-ui/src/app/core/interceptors/error.interceptor.ts: Functional HTTP interceptor that shows toast notifications for all HTTP errors with error-code-specific messages

### Modified

<!-- Phase 1: Backend Error Infrastructure -->
- src/TradingApp.Api/Infrastructure/Envelope.cs: Added ErrorCode (nullable string) and CorrelationId properties; updated constructor to accept both
- src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Added typed exception mappings (RateLimitException→429, SigningException→422, HyperliquidApiException); structured logging with CorrelationId; removed InvalidOperationException→502 mapping
- src/TradingApp.Api/Controllers/AccountController.cs: Removed shadow try/catch blocks; errors now flow to global filter; ProducesResponseType updated to Envelope; removed unused ILogger dependency
- src/TradingApp.Api/Program.cs: Registered CorrelationIdMiddleware before UseCors
- tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs: Updated error assertions from anonymous {error} shape to Envelope {errorMessage, correlationId} shape

<!-- Phase 2: Backend HTTP Resilience -->
- src/TradingApp.Api/TradingApp.Api.csproj: Added Microsoft.Extensions.Http.Resilience 8.0.0 package reference
- src/TradingApp.Api/Program.cs: Added retry resilience handler to HyperliquidRestClient HttpClient (5 retries, exponential backoff 1s–60s, retries on 429 and 5xx); outer timeout changed to 30s with 5s per-attempt timeout
- src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs: Replaced generic HttpRequestException throws with typed HyperliquidApiException/RateLimitException; added structured warning logging before throwing
- src/TradingApp.Api/Services/HyperliquidOrderService.cs: Replaced fragile HttpRequestException string match with typed HyperliquidApiException catch; signing errors now throw SigningException
- tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs: Updated GivenSignatureRejection test to throw HyperliquidApiException and assert SigningException is thrown

<!-- Phase 3: Backend WebSocket Resilience -->
- src/TradingApp.Api/Services/MarketDataStreamService.cs: Added IServiceScopeFactory dependency; added ResyncStateFromRestAsync method (resync orders+positions via REST after reconnect, push via SignalR); broadcast Reconnecting status before backoff delay; call resync after reconnect before resetting retry count
- tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs: Added IServiceScopeFactory/IServiceScope/IHyperliquidAccountService mocks; updated CreateService() helper; added GivenStreamService_WhenWebSocketReconnects_ThenResyncsOrdersAndPositions test

<!-- Phase 4: Frontend Error Infrastructure & Component Refactoring -->
- frontend/trading-ui/src/app/app.config.ts: Registered errorInterceptor via provideHttpClient(withInterceptors([errorInterceptor]))
- frontend/trading-ui/src/styles.scss: Added snackbar severity CSS classes (snackbar--error, snackbar--success, snackbar--warning, snackbar--info) using MDC CSS custom properties
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Replaced MatSnackBar with NotificationService; removed _formatErrorPayload; removed MatSnackBarModule import; error toast calls removed (covered by interceptor); success calls use _notifications.success()
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts: Replaced MatSnackBar with NotificationService; removed _formatErrorPayload; uses formatErrorPayload utility for inline leverageStatus; MatSnackBarModule removed
- frontend/trading-ui/src/app/features/market-data/market-data.component.scss: Replaced local SCSS error variables with global CSS custom properties (--colour-error-bg, --colour-error-text, --colour-error-border)

### Removed

## Test Results

<!-- Phase 1: Backend Error Infrastructure -->
- AccountControllerTests: 6/6 passed (updated assertions for Envelope shape)
- TradingApp.Api.Tests: 40/40 passed
- TradingApp.Infrastructure.Tests: 25/25 passed

<!-- Phase 2: Backend HTTP Resilience -->
- HyperliquidOrderServiceTests: 40/40 passed (signing test now asserts SigningException)
- TradingApp.Api.Tests: 40/40 passed (all passing including updated signing test)

<!-- Phase 3: Backend WebSocket Resilience -->
- MarketDataStreamServiceTests: 41/41 passed (1 new reconnect resync test)
- TradingApp.Api.Tests: 41/41 passed
- TradingApp.Infrastructure.Tests: 25/25 passed

<!-- Phase 4: Frontend Error Infrastructure & Component Refactoring -->
- ng build: succeeded (budget warning only, not an error)
- ng lint: all files pass
- TradingApp.Api.Tests: 41/41 passed (no regression)
- TradingApp.Infrastructure.Tests: 25/25 passed

## Issues

<!-- Phase 1: Backend Error Infrastructure -->
- AccountController does not extend ApiController (as suggested in details) because ApiController requires IMediator which AccountController doesn't use — kept ControllerBase consistent with OrdersController

<!-- Phase 2: Backend HTTP Resilience -->
- Microsoft.Extensions.Http.Resilience v8.0.0 does not expose ServiceProvider on ResilienceContext in OnRetry callback — removed that logging approach; Polly telemetry still logs retries via its built-in telemetry pipeline
- Corporate NuGet feeds (Deloitte Azure DevOps) required using explicit --source flag to restore packages

<!-- Phase 3: Backend WebSocket Resilience -->
- None

<!-- Phase 4: Frontend Error Infrastructure & Component Refactoring -->
- NotificationService initially used constructor injection — fixed to use inject() to satisfy @angular-eslint/prefer-inject rule

## Design Decisions

<!-- Phase 1: Backend Error Infrastructure -->
- AccountController kept as ControllerBase (not changed to ApiController) — ApiController base requires IMediator/IdentityService constructors which would be dead dependencies for this controller. OrdersController has the same pattern.

<!-- Phase 2: Backend HTTP Resilience -->
- HttpClient outer timeout raised to 30s (from 5s) to accommodate retry delays before the outer timeout fires; per-attempt timeout is 5s via AddTimeout()
- OnRetry logging removed (API doesn't exist in v8.0.0) — Polly telemetry emits retry events automatically

<!-- Phase 3: Backend WebSocket Resilience -->
- None — implemented exactly as specified

<!-- Phase 4: Frontend Error Infrastructure & Component Refactoring -->
- DashboardComponent polling errors (from forkJoin with catchError) still show explicit notifications since HTTP interceptor never fires for swallowed errors
- OrderEntryComponent: error toast on submission removed (interceptor handles); isSubmitting=false still set in error callback for UI state

## Review Hints

- HttpGlobalExceptionFilter: SigningException maps to 422 (UnprocessableEntity) — the spec chose this to distinguish it from network errors; could also be argued as 500
- DashboardComponent: error handlers for cancel/modify/close now have empty error callbacks (no inline notification) — relies entirely on HTTP interceptor for error messages; verify this UX is desired
- HTTP interceptor shows toast for ALL HTTP errors including leverage errors from OrderEntryComponent, which also sets inline leverageStatus text — this means leverage errors show both a toast AND inline text (by design)

## Release Summary

F8 — Error Handling & Resilience hardening pass completed across all 4 phases:

**Backend Error Infrastructure (Phase 1)**: Added typed exception hierarchy (HyperliquidApiException, RateLimitException, SigningException), enhanced Envelope with ErrorCode+CorrelationId, added CorrelationIdMiddleware, updated HttpGlobalExceptionFilter with 9 typed mappings, refactored AccountController to remove shadow try/catch.

**Backend HTTP Resilience (Phase 2)**: Added Microsoft.Extensions.Http.Resilience 8.0.0 with retry pipeline (5 retries, exponential backoff 1s–60s, retries on 429/5xx). HyperliquidRestClient now throws typed exceptions. Signing errors wrapped in SigningException.

**Backend WebSocket Resilience (Phase 3)**: MarketDataStreamService broadcasts Reconnecting state before backoff and resyncs orders+positions via REST after reconnection. New reconnect test added.

**Frontend Error Infrastructure (Phase 4)**: NotificationService centralises all toast notifications. Shared formatErrorPayload/extractErrorCode utilities replace duplicated code. HTTP interceptor automatically shows typed toasts for all API errors. DashboardComponent and OrderEntryComponent refactored. MarketDataComponent error styling uses global CSS custom properties.

**Results**: 66 backend tests pass (41 API + 25 Infrastructure), ng build succeeds, ng lint passes with zero errors.
