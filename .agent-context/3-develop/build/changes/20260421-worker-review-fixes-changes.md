<!-- markdownlint-disable-file -->
# Release Changes: Worker Review Fixes

**Related Plan**: 20260421-worker-review-fixes-plan.instructions.md
**Implementation Date**: 2026-04-21

## Summary

Completed implementation for the worker review fixes across all six phases, covering security hardening, execution correctness, session lifecycle, resilience, notification quality, and legacy cleanup.

## Changes

### Added

<!-- Phase 1: Security Hardening -->
- tests/TradePilot.Worker.Tests/Services/AgentCheckInServiceTests.cs: Added focused heartbeat authentication tests that verify Authorization header behavior for configured and unconfigured agent secrets.

<!-- Phase 2: Execution & Command Correctness -->
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidExchangeSymbolMapper.cs: Added an instance adapter over the static Hyperliquid mapper so DI-based symbol mapper resolution compiles again.

<!-- Phase 4: Resilience & Safety Improvements -->
- src/TradePilot.Worker/Services/ProcessedFillKeyTracker.cs: Added a bounded, time-windowed fill-key tracker for Binance polling deduplication.
- tests/TradePilot.Worker.Tests/Services/UpdateCheckerServiceTests.cs: Added direct coverage for update-safety gating.
- tests/TradePilot.Worker.Tests/Services/LiveExecutionLoggerTests.cs: Added coverage for the capped execution-log queue behavior.
- tests/TradePilot.Worker.Tests/Services/ProcessedFillKeyTrackerTests.cs: Added retention and duplicate-prevention tests for bounded fill-key tracking.

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- tests/TradePilot.Worker.Tests/Services/DynamicTelegramNotifierTests.cs: Added focused coverage for HTML parse mode and escaping of Telegram notification content.

### Modified

<!-- Phase 1: Security Hardening -->
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Added agent secret configuration support and shared control-plane HttpClient header configuration.
- src/TradePilot.Worker/Program.cs: Switched control-plane HttpClient registration to options-based configuration and bearer header injection when a secret is configured.
- src/TradePilot.Worker/appsettings.json: Added a non-sensitive Agent SecretKey placeholder.
- src/TradePilot.Worker/appsettings.Development.json: Removed the hardcoded Azure SQL connection string with cleartext credentials.

<!-- Phase 2: Execution & Command Correctness -->
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Fixed CancelTriggerOrder routing through the exchange-aware resolver and added failed command results for HandleStartAsync early exits.
- src/TradePilot.Worker/Services/UserEventStreamService.cs: Added a startup guard that skips user event streaming when no wallet is configured.
- src/TradePilot.Application/Trading/Services/FillProcessor.cs: Changed OnFillProcessed to an awaited async callback and awaited both invocation sites.
- src/TradePilot.Worker/Services/TradingSession.cs: Replaced the null-forgiving order tracker assignment with a safe fallback tracker and removed nullable clearing usage.
- tests/TradePilot.Application.Tests/Trading/Services/FillProcessorTests.cs: Updated callback typing and added a test proving ProcessFillAsync awaits the async fill callback.
- tests/TradePilot.Worker.Tests/Services/UserEventStreamServiceTests.cs: Updated signer mocking to ISignerProvider and added a wallet-unconfigured startup test.
- tests/TradePilot.Worker.Tests/Services/AgentCheckInServiceTests.cs: Added tests for CancelTriggerOrder exchange-aware routing and HandleStartAsync wallet failure result reporting.
- src/TradePilot.Worker/Program.cs: Switched Hyperliquid symbol mapper DI registration to the new adapter to unblock the required build.
- src/TradePilot.Api/Program.cs: Switched the API Hyperliquid symbol mapper DI registration to the same adapter for consistency with the mapper refactor.

<!-- Phase 3: Session Lifecycle & Risk State -->
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Replaced the async-unsafe session lock with SemaphoreSlim, serialized start-stop-heartbeat access, and reset the risk engine when creating a new session.
- src/TradePilot.Application/Abstractions/Services/IRiskEngine.cs: Added the default Reset contract for session-scoped risk state.
- src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs: Implemented Reset to clear loss history, tracked risks, order count, equity, and drawdown state.
- src/TradePilot.Application/Backtesting/Services/BacktestRiskEngine.cs: Implemented Reset to clear backtest risk and drawdown state.
- tests/TradePilot.Worker.Tests/Services/AgentCheckInServiceTests.cs: Added coverage for heartbeat lock coordination and risk-engine reset on session creation.
- tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs: Added a reset-state regression test for the live risk engine.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs: Added a reset-state regression test for the backtest risk engine.
- tests/TradePilot.Infrastructure.Tests/Services/ExchangeAbstractionAdaptersTests.cs: Updated the stale Hyperliquid mapper test to use the adapter class so the required full solution build could pass.

<!-- Phase 4: Resilience & Safety Improvements -->
- src/TradePilot.Worker/Services/UpdateCheckerService.cs: Added explicit active-session update gating and switched update downloads to a named IHttpClientFactory client.
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Synchronized network config changes, rebuilt active Hyperliquid sessions on network flips, and exposed the seam needed for focused testing.
- src/TradePilot.Worker/Services/LiveExecutionLogger.cs: Added a 10,000-entry cap that drops oldest buffered logs first.
- src/TradePilot.Worker/Services/TradingSession.cs: Replaced the unbounded Binance fill-key HashSet with the bounded retention tracker.
- src/TradePilot.Worker/Program.cs: Registered the named UpdateDownload HttpClient with the required 10-minute timeout.
- tests/TradePilot.Worker.Tests/Services/AgentCheckInServiceTests.cs: Added focused coverage for network config application without an active session.

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- src/TradePilot.Worker/Services/MarketDataStreamService.cs: Added periodic 24h volume re-seeding and jittered reconnect backoff.
- src/TradePilot.Worker/Services/NotificationDispatcher.cs: Split fill routing by channel, added SignalR fan-out for batch fills, and deduplicated SignalR publishes across single and batch paths.
- src/TradePilot.Worker/Services/UserEventStreamService.cs: Passed cancellation tokens through dispatcher callbacks and added jittered reconnect backoff.
- src/TradePilot.Worker/Services/TradingSession.cs: Added jitter to the trading-session WebSocket reconnect loop.
- src/TradePilot.Worker/Services/DynamicTelegramNotifier.cs: Switched Telegram messages from Markdown to HTML parse mode and replaced escaping accordingly.
- src/TradePilot.Worker/Services/HealthMonitorService.cs: Implemented healthy-log throttling every 10 checks and reset behavior on idle or unhealthy states.
- tests/TradePilot.Worker.Tests/Services/NotificationDispatcherTests.cs: Updated notification-routing expectations and added duplicate-suppression coverage.
- tests/TradePilot.Worker.Tests/Services/UserEventStreamServiceTests.cs: Added batch callback coverage and assertions that callback dispatches receive cancellable tokens.
- tests/TradePilot.Worker.Tests/Services/MarketDataStreamServiceTests.cs: Added regression coverage for periodic volume re-seeding.
- tests/TradePilot.Worker.Tests/Services/HealthMonitorServiceTests.cs: Updated healthy-log expectations to reflect 10-check throttling.

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- src/TradePilot.Worker/Program.cs: Added clarifying comments that the 5-second Polly timeout is per HTTP attempt in each worker resilience pipeline.

### Removed

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- src/TradingApp.Worker/Services/UserEventStreamService.cs: Removed the obsolete duplicate legacy worker implementation.
- tests/TradingApp.Worker.Tests/Services/UserEventStreamServiceTests.cs: Removed the obsolete duplicate legacy worker test coverage.

## Test Results

<!-- Phase 1: Security Hardening -->
- TradePilot.Worker.Tests.csproj build: PASSED
- TradePilot.Worker.Tests: 42/42 passed
- Architecture Tests: Not run — not required in Phase 1

<!-- Phase 2: Execution & Command Correctness -->
- TradePilot.Worker.Tests: 45/45 passed
- TradePilot.Application.Tests: 627/627 passed
- Architecture Tests: Not run — not required in Phase 2

<!-- Phase 3: Session Lifecycle & Risk State -->
- Solution Build: PASSED
- Focused Phase 3 tests: 50/50 passed
- TradePilot.Worker.Tests: 47/47 passed
- TradePilot.Application.Tests: 629/629 passed
- TradePilot.Domain.Tests: 97/97 passed
- Architecture Tests: Not run — not required in this phase

<!-- Phase 4: Resilience & Safety Improvements -->
- TradePilot.Worker.Tests.csproj build: PASSED
- TradePilot.Worker.Tests: 53/53 passed
- Architecture Tests: Not run — not required in this phase

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- TradePilot.Worker.Tests.csproj build: PASSED
- TradePilot.Worker.Tests: 58/58 passed
- Architecture Tests: Not run — not required in this phase

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- Solution Build: PASSED
- All Solution Tests: 1276/1276 passed
- TradePilot.Indicators.Tests: PASSED
- TradePilot.Domain.Tests: PASSED
- TradePilot.AI.Tests: PASSED
- TradePilot.Infrastructure.Tests: PASSED
- TradePilot.Application.Tests: PASSED
- TradePilot.Persistence.Tests: PASSED
- TradePilot.Worker.Tests: PASSED
- TradePilot.Api.Tests: PASSED
- Architecture Tests: Not run — not required in this phase

## Issues

<!-- Phase 1: Security Hardening -->
- The VS Code test runner reported a generic project build failure for the worker test scope, but direct `dotnet build` and `dotnet test` verification on the worker test project passed cleanly.

<!-- Phase 2: Execution & Command Correctness -->
- The worker test-project build initially failed because an existing in-flight refactor had made HyperliquidAssetMapper static while Worker and API DI still registered it as IExchangeSymbolMapper. This was resolved by adding `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidExchangeSymbolMapper.cs` and updating both registrations.

<!-- Phase 3: Session Lifecycle & Risk State -->
- The required `dotnet build TradePilot.sln --no-restore` initially failed because `tests/TradePilot.Infrastructure.Tests/Services/ExchangeAbstractionAdaptersTests.cs` still instantiated the now-static HyperliquidAssetMapper. Updating that stale test to use HyperliquidExchangeSymbolMapper resolved the build.

<!-- Phase 4: Resilience & Safety Improvements -->
- The initial worker test build failed because the new test files were missing TradePilot.Worker namespace imports; adding the correct using directives resolved the build.
- The runTests tool reported generic worker project build failures and did not accept the csproj path directly for the full-project run, so final phase verification used direct dotnet build and dotnet test on the worker test project.

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- The first pass at the fill-routing split removed duplicate Telegram sends but still allowed duplicate SignalR publishes when both single-fill and batch-fill callbacks carried the same fill. Adding short-lived SignalR deduplication in NotificationDispatcher resolved the overlap and is covered by a regression test.

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- TradingApp.Worker and TradingApp.Worker.Tests were already absent from TradePilot.sln, so no solution-file edit was needed. The remaining duplicate source and test directories were removed directly.
- No active Docker, CI, or solution references to TradingApp.Worker remained outside plan and review documentation.

## Design Decisions

<!-- Phase 1: Security Hardening -->
- The control-plane client only sends Authorization when Agent:SecretKey is configured, preserving local development behavior when no shared secret is set.
- The bearer header is configured on the named control-plane HttpClient so all control-plane requests from the worker use the same authentication policy consistently.

<!-- Phase 2: Execution & Command Correctness -->
- TradingSession kept its optional orderTracker constructor shape and now falls back to an in-memory tracker instead of converting the parameter to required, which avoided a broader constructor signature break while removing the null-forgiving risk.
- HandleStartAsync failure reporting uses the existing Detail field on OrderCommandResult, matching the current agent heartbeat contract instead of introducing a new error field.
- The Hyperliquid mapper build fix used a thin adapter around the new static utility rather than reverting the mapper refactor.

<!-- Phase 3: Session Lifecycle & Risk State -->
- Session lifecycle coordination now uses a single SemaphoreSlim(1, 1) across start, stop, and heartbeat reads so async session shutdown cannot race with session replacement.
- TradingSession.Start() still runs outside the lock so long-running session startup does not hold the mutual-exclusion gate longer than necessary.
- IRiskEngine.Reset() was added as a default interface method and implemented concretely in live and backtest engines to avoid breaking other implementations while still clearing singleton-held state at session creation.

<!-- Phase 4: Resilience & Safety Improvements -->
- Extracted fill-key retention into `src/TradePilot.Worker/Services/ProcessedFillKeyTracker.cs` instead of embedding more time-based state directly into TradingSession, which kept the TradingSession change small and made the retention behavior directly testable.
- Network-change restarts are limited to active Hyperliquid sessions because the mutated network configuration only affects Hyperliquid REST and WebSocket clients.
- Kept the update-safety method as an internal seam so the new resilience behavior can be tested directly without broadening the public surface area.

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- Kept the existing INotificationDispatcher surface instead of introducing new channel-specific methods and moved the routing split plus deduplication into NotificationDispatcher to minimize interface churn across the worker.
- Updated the volume re-seed timestamp in a finally block in MarketDataStreamService so transient REST failures do not trigger a new re-seed attempt every 500ms aggregation tick.

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- Kept the cleanup minimal because the legacy projects were already removed from the solution, so the phase only removed stale duplicate files and documented the timeout behavior in the active worker host.
- Added the timeout-intent comment at each 5-second AddTimeout call site so the per-attempt semantics are explicit for all three worker HTTP pipelines.

## Review Hints

<!-- Phase 1: Security Hardening -->
- Review the corresponding API heartbeat endpoint validation to ensure the shared secret sent by the worker is enforced before relying on this hardening in non-dev environments.

<!-- Phase 2: Execution & Command Correctness -->
- Review the mapper refactor seam around `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidExchangeSymbolMapper.cs` to confirm the static utility plus DI adapter split is the intended long-term pattern.
- Review any control-plane logic that interprets OrderCommandResult.Detail strings to ensure the new HandleStartAsync failure messages match expected operator feedback.

<!-- Phase 3: Session Lifecycle & Risk State -->
- Review the serialized session-access behavior in `src/TradePilot.Worker/Services/AgentCheckInService.cs` to confirm the intended tradeoff between race safety and temporary heartbeat or command waiting during session shutdown.
- Review the reset coverage in `tests/TradePilot.Worker.Tests/Services/AgentCheckInServiceTests.cs` and `tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` if you want extra confidence around future changes to session-scoped risk fields.

<!-- Phase 4: Resilience & Safety Improvements -->
- Review the restart path in `src/TradePilot.Worker/Services/AgentCheckInService.cs` under a live Hyperliquid network flip to confirm the stop-rebuild-start sequence matches the intended operator experience during rare control-plane network changes.

<!-- Phase 5: Notifications, Data Accuracy & Code Quality -->
- Review the one-minute SignalR fill deduplication window in `src/TradePilot.Worker/Services/NotificationDispatcher.cs` to confirm it matches the expected timing relationship between Hyperliquid single-fill and batch-fill callbacks.

<!-- Phase 6: Legacy Cleanup & Final Verification -->
- Review the per-attempt timeout comments in `src/TradePilot.Worker/Program.cs` if you want to confirm that retry-extended total operation time is still the intended resilience policy for Hyperliquid and Binance calls.

## Release Summary

Implemented 6 of 6 phases and completed all 32 planned tasks. The worker now removes source-controlled secrets, authenticates control-plane heartbeats when configured, routes exchange commands correctly, awaits fill post-processing, resets risk state between sessions, blocks auto-updates during active trading sessions, bounds in-memory growth for execution logs and fill deduplication, improves notification routing and Telegram escaping, adds reconnect jitter, removes the obsolete legacy worker duplicate, and passes a full solution build plus full solution test run.

Finding #17 from the original review remains intentionally deferred as planned: the TradingSession constructor-size refactor was left for a separate change because it is structural rather than corrective.
