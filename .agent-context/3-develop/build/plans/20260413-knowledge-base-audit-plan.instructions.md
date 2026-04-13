applyTo: ".agent-context/3-develop/build/changes/20260413-knowledge-base-audit-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-13T18:42:52Z"
status: "implemented"
lastUpdated: "2026-04-13T22:10:14Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Knowledge Base Audit & Refresh

## Overview

Comprehensive audit and update of all `.agent-context/0-knowledge/` files to align documentation with the actual codebase as built. The project's knowledge base was written before development began and most files are now out of date. Reviews and research documents are left in place. A "Future Recommendations" section is added to each updated file.

## Objectives

- Bring all knowledge files in line with the actual codebase
- Remove aspirational content that was never implemented (or clearly label it as future)
- Add "Future Recommendations" sections where appropriate
- Document new features/components that exist in code but have no knowledge coverage
- Remove security-sensitive content (credentials in docs)
- Update README.md index for any new/changed files
- Leave review/research docs (25, 32, 34-kronos, innovative-features/) unchanged

### Discovery References

Six parallel Code Researcher subagents audited ~35 knowledge files against the full codebase. Key findings:

**Critical Misalignments:**
- Business Model Option C chosen but not recorded anywhere
- `UserExchangeCredential` → `UserWalletAddress` (private keys never on server)
- Authentication fully implemented (JWT + Google SSO) but ADR 7 says "deferred"
- ERD has ~12 entities that don't exist and is missing ~8 that do
- Charting doc describes a line chart; actual is a 3-pane candlestick chart
- Multiple signals documented as implemented (Hedge, Cooldown, Flatten) are never emitted
- `StrategyRun`, `StrategyPerformance`, `Signal` (persisted) entities don't exist
- Strategy Optimizer is a major feature with zero knowledge coverage
- Risk engine is no longer a passthrough in backtesting

**Security Issue:**
- Google OAuth Client Secret is in plaintext in `34-google-sso-authentication.md`

### Project Patterns

- `.agent-context/0-knowledge/README.md` - Table of contents for all knowledge files
- `.github/instructions/agent-knowledge.instructions.md` - Knowledge documentation standards

### [x] Phase 1: Foundation & Architecture (00, 03, 06, 10)

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Update `00-project-overview.md` — record Option C decision, add Indicators project, add auth system, add optimization feature, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-01-details.md#task-11-update-project-overview

- [x] Task 1.2: Update `03-infrastructure-architecture.md` — remove Phase 1 VPS framing, fix SQLite paths, Azure SignalR not Redis, document Bicep infrastructure, add conditional service registration, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-01-details.md#task-12-update-infrastructure-architecture

- [x] Task 1.3: Update `06-project-structure.md` — add Indicators project, fix Worker description, add all new Application/Infrastructure/Api/Frontend components
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-01-details.md#task-13-update-project-structure

- [x] Task 1.4: Update `10-architecture-decisions.md` — fix ADR 7 (auth implemented), ADR 8 (Option C keys), ADR 13 (real identity), add new ADRs (Option C, network routing, Azure SignalR, third LLM client, macro calendar gate, optimization, indicators project)
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-01-details.md#task-14-update-architecture-decisions

### [x] Phase 2: Domain Model & Data (04, ERD)

**Complexity**: High | **Risk**: Low

- [x] Task 2.1: Update `04-domain-model.md` — replace `UserExchangeCredential` with `UserWalletAddress`, fix User/Subscription entity fields, remove non-existent entities (Order, Fill, Signal, BotState), add GridCycle/LiveOrder/LiveFill/MacroEvent/MacroSyncRun/OptimizationRun/OptimizationResult/StrategyReview, note mutable setter inconsistency, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-02-details.md#task-21-update-domain-model

- [x] Task 2.2: Rewrite `data-model/data-model-erd.md` — rebuild ERD from actual entities: User, Subscription, UserWalletAddress, Strategy, StrategyRevision, StrategyReview, Candle, FundingRate, BacktestRun, GridCycle, LiveOrder, LiveFill, LlmContextSnapshot, MacroEvent, MacroSyncRun, OptimizationRun, OptimizationResult
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-02-details.md#task-22-rewrite-erd

### [x] Phase 3: Strategy & Execution (01, 12, 13, 14, 15, 16, 19)

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Update `01-trading-strategy.md` — fix interface name (`IStrategyEngine`), remove unimplemented EMA/bias/pullback/hedge filters, document actual regime gating (MarketRegime enum), document Signal mode, add DrawdownEvaluator, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-31-update-trading-strategy

- [x] Task 3.2: Update `12-strategy-customisation.md` — fix RevisionSource → StrategyEntryPoint enum values, fix PositionSizeType naming, remove StrategyRun/StrategyPerformance references, add StrategyReview feature, add HighWaterMarkUsd, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-32-update-strategy-customisation

- [x] Task 3.3: Update `13-strategy-config-schema.md` — fix PriceVsEmaParams field names, add SupportResistance condition type, add TrendFilterConfig.Period, fix EntryMode casing documentation, note casing inconsistency bug, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-33-update-strategy-config-schema

- [x] Task 3.4: Update `14-strategy-runtime-model.md` — expand IRiskEngine interface, fix IMarketContextBuilder signatures, fix constructor taking IStrategyConfig not JSON, remove StrategyRun/StrategyPerformance, add SignalController, add HighWaterMarkUsd, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-34-update-strategy-runtime-model

- [x] Task 3.5: Update `15-grid-controller.md` — remove GridPlanner reference, mark Planning state as unused, fix output signal list (only DeployGrid + TakeProfit emitted), add ProtectionOrders state, add TrailingStopHighWatermark, fix AtrInitial default multiplier, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-35-update-grid-controller

- [x] Task 3.6: Update `16-signal-contracts.md` — mark hedge/pause/cooldown/flatten signals as NOT IMPLEMENTED, add OpenPosition signal, expand TakeProfit payload (size, orderType, gridCycleId, cancellationReason), remove signal persistence table, add CancellationReason enum, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-36-update-signal-contracts

- [x] Task 3.7: Update `19-scheduling-architecture.md` — fix constructor signature (IStrategyConfig not JSON, 11 params), fix BuildAsync not Build, add drawdown state management, add ISignalController dispatch, add ResolveAccountEquity, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-03-details.md#task-37-update-scheduling-architecture

### [x] Phase 4: Exchange, AI & Backtesting (02, 17, 18, 24-backtesting, 24-interpreter, 33)

**Complexity**: High | **Risk**: Low

- [x] Task 4.1: Update `02-hyperliquid-integration.md` — fix AccountService location, fix signer pattern (MutableSignerProvider), fix retry policy (30s + Polly), fix AssetMapper behavior, add UserEventClient, add ISignerProvider, add AssetMetadataCache, add NetworkRouting, add LiveExecutionEngine, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-41-update-hyperliquid-integration

- [x] Task 4.2: Update `17-llm-context-sentiment-architecture.md` — add DerivedRegime field, fix GeneratedAtUtc type, fix MacroRegime values, document three separate LLM clients, mark data sources as aspirational, add SyntheticRegimeProvider as primary regime classifier, add conditional registration, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-42-update-llm-context-architecture

- [x] Task 4.3: Update `18-backtesting-architecture.md` — add BacktestConfig.TriggerTimeframe, add missing BacktestResult fields, add BacktestRun progress fields, update RiskEngine (not passthrough), add async job queue architecture, add BacktestExecutionContextAccessor, add drawdown-tier gating, add onProgress callback, add ReplayData overload, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-43-update-backtesting-architecture

- [x] Task 4.4: Update `24-backtesting-grid-engine-explained.md` — fix GridStrategyEngine regime gating, fix RiskEngine (not passthrough), add DrawdownBlockedSignalCount, add ISignalController mention, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-44-update-backtesting-grid-engine

- [x] Task 4.5: Update `24-strategy-interpreter-architecture.md` — fix default provider details, add cross-reference to StrategyReviewer and LlmContextClient systems, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-45-update-strategy-interpreter

- [x] Task 4.6: Update `33-risk-management-and-trade-sizing.md` — mark portfolio heat and drawdown tiers as IMPLEMENTED (not proposed), add DrawdownScalingFactor/IsDrawdownCircuitBreakerTripped, add PassThroughRiskEngine, add ResolveInitialR, add Future Recommendations for Kelly/SQN/partial-close
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-04-details.md#task-46-update-risk-management

### [x] Phase 5: Frontend & UI (07, 09, 11)

**Complexity**: Medium | **Risk**: Low

- [x] Task 5.1: Update `07-ui-design.md` — fix dashboard description (no chart/signals/bot-state), add wizard page, add AI review feature, add NL input, add SupportResistance condition, add auth pages, add all new routes, add agents page, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-05-details.md#task-51-update-ui-design

- [x] Task 5.2: Rewrite `09-charting-library.md` — replace line chart description with candlestick chart, document 3-pane architecture, document indicator toggles (EMA/Bollinger/RSI/MACD), document fill markers, document loadMoreCandles, remove grid overlay section (not implemented), add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-05-details.md#task-52-rewrite-charting-library

- [x] Task 5.3: Update `11-angular-instructions.md` — fix primary color (cyan not green), add full CSS token table (24 tokens), add guards/interceptors/services documentation, add component architecture patterns, add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-05-details.md#task-53-update-angular-instructions

### [x] Phase 6: Operations, Business & Control Plane (05, 08, 20, 21-azure, 22, 26, 28, 29, 30, 31, 34-google-sso)

**Complexity**: Medium | **Risk**: Low

- [x] Task 6.1: Update `05-feature-specification.md` — add implemented features (optimizer, interpreter, wizard, help system, Google SSO), mark admin/billing as NOT IMPLEMENTED, update subscription status (free-only), add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-61-update-feature-specification

- [x] Task 6.2: Update `08-development-plan.md` — mark completed phases, note what was delivered beyond plan, add Phase 6 partial status (kill switch yes, auto circuit breaker no), add Future Recommendations
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-62-update-development-plan

- [x] Task 6.3: Update `20-business-model-options.md` — record Option C as the chosen model with implementation evidence
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-63-update-business-model-options

- [x] Task 6.4: Update `21-azure-deployment-infrastructure.md` — fix .NET version (10 not 8), add Worker installer pipeline, add GITHUB_TOKEN vs GHCR_PAT distinction
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-64-update-azure-deployment

- [x] Task 6.5: Update `22-prelaunch-checklist.md` — mark completed items with ✅, clearly identify remaining gaps (emergency flatten, auto circuit breaker, stuck-order detection, admin UI)
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-65-update-prelaunch-checklist

- [x] Task 6.6: Update `26-architecture-review.md` — update risk statuses (key security resolved by Option C, LLM has SyntheticRegimeProvider fallback), add new architectural components
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-66-update-architecture-review

- [x] Task 6.7: Update `28-macro-calendar.md` — add MacroEventRiskCheck integration into live trading pipeline
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-67-update-macro-calendar

- [x] Task 6.8: Update `29-control-plane-agent-architecture.md` — add HeartbeatResponse update fields, add AgentHeartbeat version/update fields, add UpdateCheckerService documentation, fix session creation description
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-68-update-control-plane-architecture

- [x] Task 6.9: Update `30-worker-execution-pipeline.md` — add conditional service registration, add UpdateCheckerService to service table, add ITriggerOrderManager, add DrawdownTiers, fix BackgroundService table
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-69-update-worker-execution-pipeline

- [x] Task 6.10: Update `31-atr-calculation.md` — minimal update (fix AtrInitial default multiplier note if needed)
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-610-update-atr-calculation

- [x] Task 6.11: Fix `34-google-sso-authentication.md` — REMOVE plaintext Client Secret, add GoogleAuthOptions config binding, add Picture field note
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-06-details.md#task-611-fix-google-sso-authentication

### [x] Phase 7: Diagrams, README & New Knowledge Files

**Complexity**: Medium | **Risk**: Low

- [x] Task 7.1: Update `diagrams/high-level-architecture.md` — split monolith box into API (control plane) + Worker (execution agent), add SignalR path, update store names
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-07-details.md#task-71-update-high-level-architecture

- [x] Task 7.2: Update `diagrams/trading-cycle-sequence.md` — fix component names to match code, add AgentCheckInService control path, add SignalController
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-07-details.md#task-72-update-trading-cycle-sequence

- [x] Task 7.3: Create `35-strategy-optimizer.md` — new knowledge file documenting the optimizer feature (sweep runner, evolutionary algorithm, walk-forward OOS validation, fitness scoring, OptimizationRun/OptimizationResult entities)
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-07-details.md#task-73-create-strategy-optimizer-knowledge

- [x] Task 7.4: Mark `21-business-model-options-legal.md` as placeholder with clear TODO status
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-07-details.md#task-74-mark-legal-placeholder

- [x] Task 7.5: Update `README.md` — add new file entries, update descriptions for changed files, add Strategy Optimizer entry
  - Details: .agent-context/3-develop/build/plans/details/20260413-knowledge-base-audit-phase-07-details.md#task-75-update-readme

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Foundation & Architecture | Medium | Low |
| Phase 2: Domain Model & Data | High | Low |
| Phase 3: Strategy & Execution | High | Medium |
| Phase 4: Exchange, AI & Backtesting | High | Low |
| Phase 5: Frontend & UI | Medium | Low |
| Phase 6: Operations, Business & Control Plane | Medium | Low |
| Phase 7: Diagrams, README & New Knowledge Files | Medium | Low |
| **Total** | **High** | **Low** |

### Scoping Notes

- All changes are to `.agent-context/0-knowledge/` files only — no source code modifications
- Review/research documents are left unchanged: `25-pine-script-integration-research.md`, `32-hyperliquid-rwa-stock-perps.md`, `34-kronos-evaluation.md`, `innovative-features/*`
- `21-business-model-options-legal.md` is marked as placeholder only (currently contains just "testing")
- `27-backtesting-date-ranges.md` has only minor date staleness — lowest priority, not included
- `23-binance-integration.md` is substantially accurate — not included (only minor gaps)
- Each file update should add a "Future Recommendations" section documenting unimplemented features and next steps
- The security fix in Task 6.11 (remove Client Secret) should be treated as high priority

## Dependencies

- Read access to all `.agent-context/0-knowledge/` files
- Read access to `src/` and `tests/` for verification (read-only)
- Agent knowledge documentation standards (`.github/instructions/agent-knowledge.instructions.md`)

## Success Criteria

- All updated knowledge files accurately reflect the current codebase
- No aspirational content is presented as implemented
- No security-sensitive data (credentials) remains in docs
- Every updated file has a "Future Recommendations" section
- README.md index is complete and accurate
- New `35-strategy-optimizer.md` file covers the optimizer feature
- ERD reflects actual database entities

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-13T18:10:44Z | 2026-04-13T18:32:32Z |
| Plan Reviewer | plan-reviewed | 2026-04-13T18:33:17Z | 2026-04-13T18:36:40Z |
| Plan Implementer | implemented | 2026-04-13T18:42:52Z | 2026-04-13T22:10:14Z |
