---
applyTo: ".agent-context/3-develop/build/changes/20260626-binance-credential-validation-and-guidance-changes.md"
currentAgent: "Implementation Planner"
agentStartedAt: "2026-06-26T19:46:49Z"
status: "planned"
lastUpdated: "2026-06-26T19:46:49Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Binance Credential Validation and Guidance

## Overview

Review and harden the Binance credential setup and validation path so failures are actionable instead of surfacing as `An unexpected error occurred`, and produce clear user-facing Binance setup instructions.

This plan focuses on the visible integration failure in the Profile UI and the immediate credential-test path. It also links to the existing unfinished trading-safety follow-up plan for deeper Binance execution fixes.

## PBI Details

The current Profile page has a `Binance API` section with saved key/secret state and a `Test Connection` action. The backend test endpoint currently calls `IBinanceFuturesAuthClient.GetBalancesAsync()`, which means the test is specifically a Binance USD-M Futures authenticated probe, not a generic Binance account or Spot API check.

That distinction matters. A Binance account can have a valid API key and still fail this application test if Futures is not enabled, the API key lacks Futures permission, the key is for testnet while the app targets production, IP restrictions do not include the host making the signed request, the account is geofenced, the server clock is skewed, or no credential is saved for the current user.

### Current State and Gaps

- `ExchangeCredentialsController.TestConnection` only supports Binance and calls the Futures auth client directly.
- The UI labels the section as generic `Binance API`, but the test is Futures-specific.
- The controller does not pre-check for a stored credential before invoking the signed client.
- The frontend credential operations handle errors locally but also allow global error notifications, which can create noisy or confusing duplicate messages.
- Frontend error formatting can fall back to `An unexpected error occurred` when the response shape is not the expected `Envelope`.
- Binance-specific error codes are not normalized into stable app-level categories for UI remediation.
- Development/testnet behavior is unclear. Current configured Futures base URL targets production `https://fapi.binance.com`; Binance USD-M Futures testnet uses a separate REST base URL.
- The existing `20260423-binance-review-round2-plan.instructions.md` remains unfinished and covers deeper execution safety issues such as cancel-after-restart.

### Decision Points

- Decide whether TradePilot's saved Binance credential is Futures-only for now or a generic Binance credential with separate Spot and Futures validation results.
- Decide which host performs signed Binance trading operations in each mode: API server, local execution agent, or both. IP allowlist guidance depends on this.
- Decide whether local/dev should target Binance Futures testnet by default or require explicit environment selection in configuration and UI.

### Recommended Direction

For the first implementation, make the current behavior explicit: label and validate it as `Binance USD-M Futures API` unless/until the product intentionally supports separate Spot and Futures credential scopes.

Then add scope-aware validation as the next step if Spot execution remains a supported product path. A combined validation response can report Spot and Futures separately without hiding that the current trading path needs Futures.

### Acceptance Criteria

- Profile page copy clearly states whether the credential being tested is Binance USD-M Futures, Spot, or both.
- Testing with no saved credential returns a clear `no_credential` response and UI message.
- Invalid keys, missing Futures permission, IP allowlist mismatch, geofence, timestamp skew, rate limit, and upstream outage are classified into stable app-level error codes.
- The UI renders actionable remediation text for known Binance failure categories.
- Credential save/test/remove operations do not show duplicate global and inline error messages.
- The UI never falls back to `An unexpected error occurred` for known credential setup failures.
- Binance environment (Production/Testnet) is visible in the UI and controlled by configuration.
- Clear user-facing Binance setup documentation is added to the app/help/docs surface.
- Existing Binance trading-safety follow-up plan is either implemented or explicitly scheduled after the credential UX fix.

## Objectives

- Turn credential validation into a supportable diagnostic flow.
- Reduce confusion for users who have a valid Binance account but not a Futures-enabled API key.
- Make production versus testnet behavior explicit.
- Preserve secure credential handling: secrets remain encrypted at rest and are never displayed after saving.
- Improve observability by surfacing correlation IDs and app-level error categories.

### Discovery References

- `.agent-context/0-knowledge/00-project-overview.md`
- `.agent-context/0-knowledge/03-infrastructure-architecture.md`
- `.agent-context/0-knowledge/10-architecture-decisions.md`
- `.agent-context/3-develop/build/plans/20260422-binance-integration-fixes-plan.instructions.md`
- `.agent-context/3-develop/build/plans/20260423-binance-review-round2-plan.instructions.md`
- `.agent-context/1-discover/code-review/binance-integration-review.md`
- `src/TradePilot.Api/Controllers/ExchangeCredentialsController.cs`
- `src/TradePilot.Api/Infrastructure/BinanceSigningHandler.cs`
- `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs`
- `frontend/trading-ui/src/app/features/profile/profile-page.component.html`
- `frontend/trading-ui/src/app/features/profile/profile-page.component.ts`
- Binance USD-M Futures API documentation for base URLs, signed requests, timing security, and HTTP error handling.

### Project Patterns

- API controllers return `Envelope` errors for known validation and domain failures.
- The frontend centralizes HTTP error notification in `error.interceptor.ts` and per-feature rendering in components.
- Exchange selection is sent via `X-Exchange` from the Angular exchange context.
- Binance authenticated clients use HMAC signing with `X-MBX-APIKEY`, timestamp, `recvWindow`, and signature.

### [ ] Phase 1: Clarify the Backend Credential-Test Contract

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 1.1: Add a stored-credential pre-check
  - Files: `src/TradePilot.Api/Controllers/ExchangeCredentialsController.cs`
  - What: Before calling `_binanceAuthClient.GetBalancesAsync`, verify the authenticated user has an active Binance credential. If not, return `BadRequest(new Envelope("No Binance API credential saved. Please save your API key and secret first.", "binance_no_saved_credential"))`.
  - Why: A missing local credential is not a Binance-side failure.

- [ ] Task 1.2: Make the validation response scope-aware
  - Files: `ExchangeCredentialsController.cs`, response models under API/Application if needed
  - What: Replace the boolean-only response with a richer result containing exchange, environment, tested scopes, endpoint family, success state, and optional warnings.
  - First implementation: `Futures` scope only, labelled explicitly as USD-M Futures.

- [ ] Task 1.3: Add configuration for Binance environment
  - Files: `BinanceTradingOptions.cs`, `BinanceSpotTradingOptions.cs`, `src/TradePilot.Api/appsettings.json`, `src/TradePilot.Api/appsettings.Development.json`, `src/TradePilot.Worker/appsettings.json`
  - What: Add an explicit environment/testnet flag or environment name and ensure base URLs are not ambiguous. Production Futures uses `https://fapi.binance.com`; Futures testnet must use the configured Binance testnet REST base URL.
  - UI impact: expose the active environment as read-only metadata on the Profile page.

- [ ] Task 1.4: Decide Spot/Futures credential semantics
  - Files: change record / knowledge docs
  - What: If Spot execution is in scope, add separate Spot validation through `IBinanceSpotAuthClient`; otherwise update product copy to say Futures-only.

### [ ] Phase 2: Normalize Binance Errors into App-Level Categories

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 2.1: Add stable Binance credential error codes
  - Files: `src/TradePilot.Application/Abstractions/Exceptions/BinanceApiException.cs`, `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs`, `HttpGlobalExceptionFilter.cs`
  - What: Map known Binance responses into stable codes such as `binance_invalid_credentials`, `binance_missing_futures_permission`, `binance_ip_not_whitelisted`, `binance_geofenced`, `binance_timestamp_skew`, `binance_rate_limited`, `binance_ip_banned`, and `binance_upstream_unavailable`.

- [ ] Task 2.2: Preserve Binance diagnostic detail safely
  - Files: `BinanceFuturesAuthClient.cs`, `Envelope.cs` if extension is needed
  - What: Keep sanitized Binance code/message and correlation ID available to the UI/support logs without exposing secrets or signed payloads.

- [ ] Task 2.3: Treat timeout/unknown execution status carefully
  - Files: Binance auth/execution clients
  - What: For order endpoints, never assume Binance HTTP 503/timeout means an order failed. For the read-only balance credential test, classify upstream timeout as retryable/unavailable.
  - Why: Binance documentation distinguishes unknown execution status from confirmed failure.

- [ ] Task 2.4: Add controller and exception-filter tests
  - Files: `tests/TradePilot.Api.Tests/Controllers/ExchangeCredentialsControllerTests.cs`, `tests/TradePilot.Api.Tests/Services/BinanceFuturesAuthClientTests.cs`
  - Cases: no credential, invalid key, missing permission, IP whitelist issue, geofence 451, 429/418, 403, timestamp skew, 5xx/503, success.

### [ ] Phase 3: Improve Frontend Error Handling and Guidance

**Complexity**: Medium | **Risk**: Low

- [ ] Task 3.1: Stop duplicate notifications for credential actions
  - Files: `frontend/trading-ui/src/app/core/services/exchange-credentials.service.ts`, `profile-page.component.ts`
  - What: Use the existing skip-notification context for save/test/remove calls when the Profile page renders inline credential status itself.

- [ ] Task 3.2: Harden error formatting fallback
  - Files: `frontend/trading-ui/src/app/core/utils/error-utils.ts`, `error.interceptor.ts`
  - What: Recognize `errorMessage`, `message`, `detail`, `title`, nested error objects, plain text responses, and network status `0` before using the generic fallback.

- [ ] Task 3.3: Render Binance-specific remediation
  - Files: `profile-page.component.ts`, `profile-page.component.html`, `profile-page.component.scss`
  - What: Show targeted messages for invalid credentials, missing Futures permission, IP allowlist mismatch, geofence, timestamp skew, rate limit, and upstream unavailable. Include correlation ID where available.

- [ ] Task 3.4: Show active Binance environment and validation scope
  - Files: Profile page component/service models
  - What: Display `Production` or `Testnet` and `USD-M Futures` next to the credential form and in the test success message.

- [ ] Task 3.5: Guard exchange switching when Binance is unconfigured
  - Files: exchange context service or dashboard/order-entry components
  - What: If the user selects Binance without a saved credential, show a clear prompt linking to Profile instead of letting dashboard polling fail repeatedly.

### [ ] Phase 4: Add User-Facing Binance Setup Instructions

**Complexity**: Low | **Risk**: Low

- [ ] Task 4.1: Add documentation in the product/help surface
  - Files: `docs/` or existing Help content source, Profile page help panel if applicable
  - What: Add instructions for creating a Binance API key, enabling USD-M Futures, selecting correct environment, setting permissions, IP restrictions, and troubleshooting common errors.

- [ ] Task 4.2: Add concise inline setup guidance
  - Files: `profile-page.component.html`
  - What: Add short inline guidance without turning the form into a wall of text. Link to the full instructions.

- [ ] Task 4.3: Add operator/developer notes for IP allowlisting
  - Files: docs/operations or knowledge docs
  - What: Document which outbound IP must be whitelisted depending on whether signed calls are made by Azure API, local execution agent, or both.

### Draft User Setup Instructions

The implemented documentation should cover the following points:

- Create or use a verified Binance account in a region where Binance and Futures are available.
- Activate USD-M Futures in Binance before testing TradePilot if the app is configured for Futures.
- Create an API key type supported by TradePilot's signing implementation. Current implementation expects HMAC key/secret signing.
- Enable Reading and Futures permissions. Enable trading only when ready for live execution. Do not enable withdrawals.
- Use a production key with the production endpoint and a testnet key with the testnet endpoint; they are not interchangeable.
- If IP restrictions are enabled, whitelist the outbound IP of the host that makes signed Binance calls. For the current Profile test that is the API host; for local agent execution it may also be the user's machine/agent host.
- Keep the server/agent clock synchronized because signed Binance requests require a valid timestamp and `recvWindow`.
- Rotate the Binance key if the secret is exposed or regenerated. The secret cannot be retrieved from TradePilot after saving.
- Common failures:
  - `-2015` or unauthorized: wrong key/secret, missing permission, wrong environment, or IP restriction.
  - `403`: blocked/forbidden request or WAF-style restriction.
  - `451`: region/geofence restriction.
  - `429`: rate limit; wait and retry.
  - `418`: temporary IP ban after repeated rate-limit violations.
  - timestamp/recvWindow error: clock skew or high latency.

### [ ] Phase 5: Schedule Remaining Binance Trading-Safety Work

**Complexity**: High | **Risk**: High

- [ ] Task 5.1: Resume or supersede the Round 2 Binance safety plan
  - Files: `.agent-context/3-develop/build/plans/20260423-binance-review-round2-plan.instructions.md`
  - What: Implement or re-review the existing plan for cancel-after-restart rehydration, exchange-authoritative leverage brackets, bounded-parallel fills, and non-power-of-ten order normalization.
  - Why: Better credential UX does not resolve deeper execution safety risks.

- [ ] Task 5.2: Add integration-style validation against Binance testnet where feasible
  - Files: tests/scripts/docs
  - What: Add an opt-in test script or documented checklist that uses testnet credentials from environment variables and never runs in normal CI by default.

- [ ] Task 5.3: Update Binance knowledge docs after implementation
  - Files: `.agent-context/0-knowledge/23-binance-integration.md` if present
  - What: Record final product semantics for Binance Spot/Futures, credential storage, signing host, testnet support, and known operational limitations.

## Validation Steps

- No saved credential: `POST /api/credentials/Binance/test` returns a clear `binance_no_saved_credential` response.
- Invalid secret: UI shows invalid key/secret remediation, not `An unexpected error occurred`.
- Spot-only key against Futures validation: UI says Futures permission or USD-M Futures activation is required.
- Testnet key against production config: UI says environment mismatch is likely.
- IP-restricted key from an unlisted host: UI mentions IP allowlisting.
- Geofenced response: UI explains region restriction without implying a local code bug.
- Rate limit/ban: status and retry guidance are clear.
- Timestamp skew: instructions mention NTP/clock sync.
- Successful Futures credential: UI states that USD-M Futures was validated against the active environment.
- Dashboard exchange switch without Binance credential: user sees a Profile setup prompt instead of repeated polling errors.
- Existing known unrelated full-solution test drift around `CandlesControllerTests` is not treated as a Binance regression unless that slice is touched.

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend Credential Contract | Medium | Medium |
| Phase 2: Error Normalization | Medium | Medium |
| Phase 3: Frontend Error Handling | Medium | Low |
| Phase 4: User Instructions | Low | Low |
| Phase 5: Trading-Safety Follow-up | High | High |
| **Total** | **Medium-High** | **Medium-High** |

### Scoping Notes

- The immediate UI issue is separable from deeper execution safety defects. Do the credential/error UX first so setup is diagnosable.
- Do not over-promise Spot support if the validation and execution path is currently Futures-first.
- IP allowlisting instructions depend on whether signed requests are made from Azure API, the local agent, or both.
- Binance geofencing and account activation cannot be fixed in code; they must be surfaced clearly.
- Testnet support must be explicit because production and testnet keys/endpoints do not mix.

## Dependencies

- .NET 10 SDK and existing API test projects.
- Angular test/build tooling under `frontend/trading-ui`.
- Optional Binance testnet credentials for manual or opt-in integration validation.
- Product decision on Futures-only versus Spot+Futures credential scope.

## Success Criteria

- Known Binance setup failures produce stable app-level error codes.
- Profile UI renders actionable messages and no duplicate credential-error notifications.
- The generic `An unexpected error occurred` fallback is not used for expected Binance credential failures.
- Active Binance environment and validation scope are visible to the user.
- User-facing Binance setup instructions are available from the product or docs.
- Tests cover controller, client, and frontend error handling for the primary failure categories.
- The remaining Binance trading-safety plan is explicitly scheduled or implemented next.

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-06-26T19:46:49Z | 2026-06-26T19:46:49Z |
