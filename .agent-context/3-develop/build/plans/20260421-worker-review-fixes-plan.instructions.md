---
applyTo: ".agent-context/3-develop/build/changes/20260421-worker-review-fixes-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-21T15:51:13Z"
status: "complete"
lastUpdated: "2026-04-21T17:00:56Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Worker Review Fixes

## Overview

Address all 24 findings from the Worker Application tribunal code review, covering security hardening, correctness bugs, resilience improvements, notification routing, code quality, and legacy cleanup.

## PBI Details

Source: [worker-review.md](../../../../.agent-context/1-discover/code-review/worker-review.md)

24 findings across 3 severity levels:
- **CRITICAL** (3): Hardcoded credentials (#1), unconfigured wallet crash (#4), unauthenticated heartbeat (#9)
- **MAJOR** (13): Wrong execution engine (#2), update safety (#5), network config split (#6), notification duplication (#7), session lock race (#10), volume accumulation (#11), direct HttpClient (#12), null-forgiving order tracker (#13), risk state global (#19), async void fills (#20), unbounded dedupe set (#21), missing command result (#22)
- **MINOR** (8): Test coverage (#3), legacy duplication (#8), reconnection jitter (#14), unbounded log queue (#15), Telegram escape (#16), constructor size (#17 — deferred), timeout ambiguity (#18), health monitor divergence (#23), missing cancellation tokens (#24)

### Acceptance Criteria

- All CRITICAL and MAJOR findings are fixed with corresponding tests
- All MINOR findings are fixed (except #17 — deferred to separate PR)
- Solution builds cleanly
- All existing and new tests pass
- No regressions introduced

## Objectives

- Fix all security vulnerabilities (credential exposure, unauthenticated command channel)
- Fix all correctness bugs (execution engine routing, fill processing, session lifecycle, risk state)
- Improve resilience (update safety, network config, unbounded memory growth)
- Fix notification routing inconsistencies
- Improve code quality (jitter, escape methods, comment accuracy, cancellation tokens)
- Remove legacy duplicate code
- Add test coverage for high-risk paths

### Discovery References

All findings confirmed against source code during code review. Key domain context from:
- `.agent-context/0-knowledge/00-project-overview.md` — Option C split architecture
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — MutableSignerProvider, execution engines
- `.agent-context/0-knowledge/03-infrastructure-architecture.md` — Worker hosted services
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — IRiskEngine interface, one scheduler per session
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — per-session StrategyScheduler

### Project Patterns

- `src/TradePilot.Worker/Services/AgentCheckInService.cs` — Command dispatch, session lifecycle, heartbeat loop (~1100 lines, zero test coverage)
- `src/TradePilot.Worker/Services/TradingSession.cs` — Strategy execution, fill callback wiring, WebSocket reconnection
- `src/TradePilot.Application/Trading/Services/FillProcessor.cs` — Fill processing with Action callback (async void bug)
- `src/TradePilot.Infrastructure/Services/ExchangeExecutionEngineResolver.cs` — Exchange-aware keyed DI resolution
- `src/TradePilot.Worker/Services/TradingHealthProvider.cs` — IsTradingSessionActive already exposed
- `src/TradePilot.Worker/Services/UpdateCheckerService.cs` — Update safety logic, direct HttpClient
- `tests/TradePilot.Worker.Tests/Services/` — MSTest, Moq, FluentAssertions 6, BackgroundService test pattern

### [x] Phase 1: Security Hardening

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Remove hardcoded credentials from appsettings.Development.json
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-01-details.md#task-11-remove-hardcoded-credentials

- [x] Task 1.2: Add shared-secret authentication to agent heartbeat channel
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-01-details.md#task-12-add-heartbeat-authentication

- [x] Task 1.3: Tests for heartbeat authentication
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-01-details.md#task-13-tests-for-heartbeat-authentication

- [x] Task 1.4: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-01-details.md#task-14-build-and-run-tests

### [x] Phase 2: Execution & Command Correctness

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Fix CancelTriggerOrder execution engine resolution
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-21-fix-canceltriggerorder-execution-engine-resolution

- [x] Task 2.2: Guard UserEventStreamService against unconfigured wallet
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-22-guard-usereventstreamservice-against-unconfigured-wallet

- [x] Task 2.3: Change FillProcessor.OnFillProcessed from Action to Func of Task
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-23-change-fillprocessoronfillprocessed-from-action-to-func-of-task

- [x] Task 2.4: Report command result on HandleStartAsync failures
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-24-report-command-result-on-handlestartasync-failures

- [x] Task 2.5: Make orderTracker parameter required or add null-object fallback
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-25-fix-ordertracker-null-forgiving-operator

- [x] Task 2.6: Tests for execution and command fixes
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-26-tests-for-execution-and-command-fixes

- [x] Task 2.7: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-02-details.md#task-27-build-and-run-tests

### [x] Phase 3: Session Lifecycle & Risk State

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Fix session lock in HandleStartAsync
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-03-details.md#task-31-fix-session-lock-in-handlestartasync

- [x] Task 3.2: Add IRiskEngine.Reset and call on session start
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-03-details.md#task-32-add-iriskenginereset-and-call-on-session-start

- [x] Task 3.3: Tests for session lifecycle and risk state
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-03-details.md#task-33-tests-for-session-lifecycle-and-risk-state

- [x] Task 3.4: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-03-details.md#task-34-build-and-run-tests

### [x] Phase 4: Resilience & Safety Improvements

**Complexity**: Medium | **Risk**: Medium

- [x] Task 4.1: Gate IsSafeToUpdate on IsTradingSessionActive
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-41-gate-issafetoupdate-on-istradingsessionactive

- [x] Task 4.2: Fix network config mutation with atomic swap and client rebuild
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-42-fix-network-config-mutation

- [x] Task 4.3: Use IHttpClientFactory in UpdateCheckerService
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-43-use-ihttpclientfactory-in-updatecheckerservice

- [x] Task 4.4: Cap LiveExecutionLogger queue
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-44-cap-liveexecutionlogger-queue

- [x] Task 4.5: Add retention to Binance processedFillKeys
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-45-add-retention-to-processedfillkeys

- [x] Task 4.6: Tests for resilience improvements
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-46-tests-for-resilience-improvements

- [x] Task 4.7: Build and run tests
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-04-details.md#task-47-build-and-run-tests

### [x] Phase 5: Notifications, Data Accuracy & Code Quality

**Complexity**: Low | **Risk**: Low

- [x] Task 5.1: Fix 24h volume accumulation with periodic re-seed
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-51-fix-24h-volume-accumulation

- [x] Task 5.2: Fix fill notification duplication
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-52-fix-fill-notification-duplication

- [x] Task 5.3: Add WebSocket reconnection jitter
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-53-add-websocket-reconnection-jitter

- [x] Task 5.4: Fix DynamicTelegramNotifier Escape method
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-54-fix-telegram-escape-method

- [x] Task 5.5: Fix health monitor comment-behavior divergence
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-55-fix-health-monitor-divergence

- [x] Task 5.6: Flow cancellation tokens in UserEventStreamService callbacks
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-56-flow-cancellation-tokens

- [x] Task 5.7: Tests and build verification
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-05-details.md#task-57-tests-and-build-verification

### [x] Phase 6: Legacy Cleanup & Final Verification

**Complexity**: Low | **Risk**: Low

- [x] Task 6.1: Remove or deprecate TradingApp.Worker legacy project
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-06-details.md#task-61-remove-legacy-tradingappworker

- [x] Task 6.2: Document resilience timeout intent
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-06-details.md#task-62-document-resilience-timeout-intent

- [x] Task 6.3: Full solution build and test verification
  - Details: .agent-context/3-develop/build/plans/details/20260421-worker-review-fixes-phase-06-details.md#task-63-full-solution-build-and-test-verification

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Security Hardening | Medium | Medium |
| Phase 2: Execution & Command Correctness | Medium | Medium |
| Phase 3: Session Lifecycle & Risk State | High | Medium |
| Phase 4: Resilience & Safety Improvements | Medium | Medium |
| Phase 5: Notifications, Data Accuracy & Code Quality | Low | Low |
| Phase 6: Legacy Cleanup & Final Verification | Low | Low |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- Finding #17 (TradingSession 30+ parameter constructor) is **deferred to a separate PR** — it's a large structural refactor that could introduce regressions and doesn't affect correctness
- Finding #6 (network config split) fix is scoped to atomic swap + session restart rather than a full NetworkRoutingHandler DelegatingHandler port — the simpler approach is sufficient given network changes are rare control-plane events
- Finding #9 (heartbeat auth) is implemented as shared secret in this PR — mTLS or per-agent JWT is a future enhancement
- All phases include tests and build verification within the phase

## Dependencies

- .NET 10 SDK
- MSTest 3.0.4, Moq 4.20.72, FluentAssertions 6.12.2
- Existing project references (no new NuGet packages required)

## Success Criteria

- All 23 findings addressed (Finding #17 deferred with documented rationale)
- Solution builds cleanly with `dotnet build TradePilot.sln`
- All existing tests pass
- All new tests pass
- No hardcoded credentials in any tracked config file
- Agent heartbeat channel requires authentication
- CancelTriggerOrder correctly routes through exchange-aware resolver
- Worker starts cleanly without private key configured
- Fill processing is fully awaited (no async void)
- Risk engine state resets between sessions
- IsSafeToUpdate checks explicit session state
- LiveExecutionLogger and processedFillKeys have bounded memory

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-21T13:21:12Z | 2026-04-21T13:27:53Z |
| Plan Reviewer | plan-reviewed | 2026-04-21T13:33:16Z | 2026-04-21T13:37:41Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-21T14:16:53Z | 2026-04-21T15:48:19Z |
| 3-Develop: 3 Reviewer | complete | 2026-04-21T15:51:13Z | 2026-04-21T17:00:56Z |
