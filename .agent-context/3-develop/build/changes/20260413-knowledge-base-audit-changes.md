<!-- markdownlint-disable-file -->
# Release Changes: Knowledge Base Audit & Refresh

**Related Plan**: 20260413-knowledge-base-audit-plan.instructions.md
**Implementation Date**: 2026-04-13

## Summary

Knowledge-base audit and refresh across .agent-context/0-knowledge to align documentation with the implemented codebase, remove stale or insecure content, and add missing architecture coverage.

## Changes

### Added

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- .agent-context/0-knowledge/35-strategy-optimizer.md: Created a new knowledge document for the implemented optimizer architecture, lifecycle, entities, API surface, UI behavior, and extension guidance.

### Modified

<!-- Phase 1: Foundation & Architecture -->
- .agent-context/0-knowledge/00-project-overview.md: Rewrote the overview to record Option C as the chosen model and document the implemented auth, indicators, optimizer, macro calendar, and future recommendations.
- .agent-context/0-knowledge/03-infrastructure-architecture.md: Reframed infrastructure around local development plus Azure deployment, corrected storage and streaming details, and documented conditional service registration and network routing.
- .agent-context/0-knowledge/06-project-structure.md: Expanded the solution structure with the Indicators project and the current Application, API, Worker, Infrastructure, and frontend feature layout.
- .agent-context/0-knowledge/10-architecture-decisions.md: Corrected stale ADRs for authentication, identity, and key custody, and added ADRs covering Option C, Azure SignalR, network routing, separate LLM clients, macro gating, optimization, and indicators.

<!-- Phase 2: Domain Model & Data -->
- .agent-context/0-knowledge/04-domain-model.md: Replaced the stale aspirational domain description with a code-backed entity model covering implemented entities, relationship notes, schema inconsistencies, and future recommendations.
- .agent-context/0-knowledge/data-model/data-model-erd.md: Rebuilt the ERD from the actual EF Core model, removed phantom entities, and separated enforced foreign keys from application-level logical relationships.

<!-- Phase 3: Strategy & Execution -->
- .agent-context/0-knowledge/01-trading-strategy.md: Rewrote the strategy overview to document the implemented IStrategyEngine-based grid and signal paths, regime gating, drawdown tiers, and portfolio heat enforcement.
- .agent-context/0-knowledge/12-strategy-customisation.md: Corrected authoring terminology to StrategyEntryPoint and current sizing enums, removed nonexistent runtime entities, and added StrategyReview and HighWaterMarkUsd coverage.
- .agent-context/0-knowledge/13-strategy-config-schema.md: Updated schema details for current field names, SupportResistance conditions, TrendFilterConfig.Period, indicator requirements, and the known entry-mode casing mismatch.
- .agent-context/0-knowledge/14-strategy-runtime-model.md: Realigned the runtime model to the implemented scheduler, signal controller, risk engine, and persisted drawdown high-water mark flow.
- .agent-context/0-knowledge/15-grid-controller.md: Replaced stale planner and hedge descriptions with the implemented lifecycle, protection-order state, emitted signals, and ATR defaults.
- .agent-context/0-knowledge/16-signal-contracts.md: Updated signal documentation to reflect the real emitted signals, added OpenPosition and CancellationReason coverage, and marked hedge, pause, cooldown, and flatten flows as not implemented.
- .agent-context/0-knowledge/19-scheduling-architecture.md: Updated the scheduler document to the current constructor shape, BuildAsync path, drawdown application, signal-mode dispatch, ResolveAccountEquity flow, and LastContext exposure.

<!-- Phase 4: Exchange, AI & Backtesting -->
- .agent-context/0-knowledge/02-hyperliquid-integration.md: Clarified signer-interface limits, runtime-configured signing, retry behavior, network routing, and the current Hyperliquid integration surface.
- .agent-context/0-knowledge/17-llm-context-sentiment-architecture.md: Tightened the market-context documentation around the three-client AI split, conditional registration, and SyntheticRegimeProvider as the always-on classifier.
- .agent-context/0-knowledge/18-backtesting-architecture.md: Rewrote the backtesting architecture around the implemented queued-job model, progress reporting, trigger timeframe support, review summaries, and drawdown-aware risk enforcement.
- .agent-context/0-knowledge/24-backtesting-grid-engine-explained.md: Reworked the grid-engine explainer to reflect regime gating, active risk enforcement, blocked-signal counters, and the current replay pipeline.
- .agent-context/0-knowledge/24-strategy-interpreter-architecture.md: Clarified interpreter defaults and cross-referenced the separate reviewer and market-context AI systems.
- .agent-context/0-knowledge/33-risk-management-and-trade-sizing.md: Replaced contradictory legacy guidance with the current implemented risk model covering portfolio heat, drawdown tiers, PassThroughRiskEngine, and ResolveInitialR.

<!-- Phase 5: Frontend & UI -->
- .agent-context/0-knowledge/07-ui-design.md: Rewrote the UI knowledge doc to reflect the implemented route map, dashboard composition, strategy builder and wizard flows, auth pages, agents page, and current UI gaps.
- .agent-context/0-knowledge/09-charting-library.md: Replaced the stale line-chart description with the actual three-pane candlestick and equity-chart architecture, including toggles, fill markers, and historical paging.
- .agent-context/0-knowledge/11-angular-instructions.md: Expanded the Angular guidance to cover the real provider setup, guards, interceptors, services, theme palette, token set, storage model, and deployment config notes.

<!-- Phase 6: Operations, Business & Control Plane -->
- .agent-context/0-knowledge/28-macro-calendar.md: Clarified MacroEventRiskCheck as the current macro-event entry gate and added future recommendations for fuller worker-side enforcement.
- .agent-context/0-knowledge/29-control-plane-agent-architecture.md: Rewrote the control-plane and execution-agent documentation to match current heartbeat fields, update metadata, session creation, UpdateCheckerService behavior, and hosted services.
- .agent-context/0-knowledge/30-worker-execution-pipeline.md: Updated the worker execution pipeline doc for the current hosted-service topology, session lifecycle, trigger-order cleanup, drawdown tiers, and Azure SignalR behavior.
- .agent-context/0-knowledge/31-atr-calculation.md: Added future recommendations while preserving the otherwise accurate ATR behavior documentation.
- .agent-context/0-knowledge/34-google-sso-authentication.md: Removed the plaintext client secret and replaced it with config-binding, external-provider lookup, Picture field, and future recommendation guidance.

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- .agent-context/0-knowledge/diagrams/high-level-architecture.md: Rewrote the architecture diagram to show the split API control plane and worker execution agent, SignalR flow, heartbeat path, and current persistence components.
- .agent-context/0-knowledge/diagrams/trading-cycle-sequence.md: Replaced the stale monolith sequence with the current worker-driven trading cycle and control-plane heartbeat path.
- .agent-context/0-knowledge/21-business-model-options-legal.md: Replaced placeholder text with an explicit legal-work TODO checklist and placeholder status.
- .agent-context/0-knowledge/README.md: Refreshed the knowledge index, updated descriptions, added missing entries, and included the new strategy optimizer document.

### Removed

## Test Results

<!-- Phase 1: Foundation & Architecture -->
- Documentation validation: 4/4 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase contained documentation-only tasks and no build or test steps were specified in the phase details.

<!-- Phase 2: Domain Model & Data -->
- Documentation validation: 2/2 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase contained documentation-only tasks and the phase details did not specify build or test commands.

<!-- Phase 3: Strategy & Execution -->
- Documentation validation: 7/7 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase contained documentation-only tasks and the phase details did not specify build or test commands.

<!-- Phase 4: Exchange, AI & Backtesting -->
- Documentation validation: 6/6 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase contained documentation-only tasks and the phase details did not specify build or test commands.

<!-- Phase 5: Frontend & UI -->
- Documentation validation: 3/3 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase was documentation-only and the phase details did not specify build or test commands.

<!-- Phase 6: Operations, Business & Control Plane -->
- Documentation validation: 5/5 updated knowledge files passed problem scan.
- Architecture Tests: NOT RUN - this phase was documentation-only and the phase details did not specify build or test commands.

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- Documentation validation: 5/5 updated Markdown files passed problem scan via error validation.
- Architecture Tests: NOT RUN - this phase was documentation-only and the phase details did not specify build or test commands.

## Issues

<!-- Phase 1: Foundation & Architecture -->
- The repository already contained plan and change tracking workspace edits; those files were left untouched by the phase implementation work except for the required orchestrator updates.

<!-- Phase 2: Domain Model & Data -->
- The implemented schema diverges from some plan assumptions: several tenant-linked entities use string identifiers without EF foreign keys, StrategyReview is not FK-linked to StrategyRevision, and OptimizationRun does not currently store StrategyId. The docs were aligned to code and those gaps were called out explicitly.

<!-- Phase 3: Strategy & Execution -->
- None.

<!-- Phase 4: Exchange, AI & Backtesting -->
- A large markdown patch failed on context matching in the stale docs. Resolved by replacing the heavily outdated files with fresh code-aligned content using separate delete/add patch steps.

<!-- Phase 5: Frontend & UI -->
- The phase details referenced staticwebapp.config.json as deployment config, but the repo currently only contains the angular.json asset reference and proxy.conf.json. The knowledge docs were aligned to the checked-in state instead of claiming an unverified Static Web Apps config file.

<!-- Phase 6: Operations, Business & Control Plane -->
- A large multi-file patch hit context mismatches in the Google SSO document and was resolved by switching to smaller file-specific patches.
- The macro-calendar phase detail described broader live-risk wiring than the checked-in code shows; the doc now reflects MacroEventRiskCheck as the current abstraction and explicitly notes the worker-side wiring gap.
- A malformed markdown table was introduced in the worker execution pipeline doc during editing and corrected in a follow-up patch.
- Tasks 6.1 through 6.6 required audit verification only because the checked-in docs were already aligned with the phase requirements.

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- None.

## Design Decisions

<!-- Phase 1: Foundation & Architecture -->
- Documented SignalR as two runtime modes: the API hosts local streaming when Azure SignalR is absent, while the worker hosts streaming and publishes through Azure SignalR when the Azure connection string is configured.
- Updated the worker architecture documentation to reflect the implemented split-architecture model instead of the earlier centralized-worker framing.

<!-- Phase 2: Domain Model & Data -->
- Documented enforced and logical relationships separately in the ERD so the diagram reflects the real database schema without inventing foreign keys that do not exist.
- Preserved implemented schema behavior where it differs from the plan notes because the task is a codebase-aligned audit, not a target-state redesign.

<!-- Phase 3: Strategy & Execution -->
- Documented the implemented single-session scheduler model rather than the earlier multi-subscriber fan-out description because this audit aligns to code, not the target-state architecture.
- Kept the entry-mode casing mismatch and the reserved-but-unemitted signal types explicitly called out as current-state behavior so future planning can distinguish bugs and gaps from implemented features.

<!-- Phase 4: Exchange, AI & Backtesting -->
- Documented backtesting as the implemented async queue plus hosted-service model instead of the older synchronous request-scoped description.
- Treated the market-context LLM as an optional overlay and kept SyntheticRegimeProvider as the primary classifier to match the live safety model.
- Collapsed the risk-management document to current-state behavior only so the knowledge base no longer mixes implemented and aspirational guidance.

<!-- Phase 5: Frontend & UI -->
- Documented the actual implemented frontend state even where it diverges from earlier guidance, including dashboard scope, chart behavior, localStorage auth persistence, and remaining CommonModule usage.
- Recorded the full token set from styles.scss rather than preserving the older abbreviated palette so future design planning starts from the real theme baseline.

<!-- Phase 6: Operations, Business & Control Plane -->
- Treated tasks 6.1 through 6.6 as completed through audit verification because the checked-in versions of 05-feature-specification.md, 08-development-plan.md, 20-business-model-options.md, 21-azure-deployment-infrastructure.md, 22-prelaunch-checklist.md, and 26-architecture-review.md already matched the phase requirements.
- Aligned the control-plane and worker documentation to current code-backed behavior where the phase notes were more aspirational.
- Removed the Google OAuth client secret entirely and replaced it with a non-secret placeholder and binding guidance to satisfy the documentation security requirement.

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- Documented the optimizer as signal-mode-only because StrategyConfigGenerator currently emits StrategyMode.Signal configs rather than grid configs.
- Kept both 24-series architecture documents separately indexed in the README because the repository contains two distinct knowledge files with the same numeric prefix and both are actively referenced.

## Review Hints

<!-- Phase 1: Foundation & Architecture -->
- Review the conditional streaming section in 03-infrastructure-architecture.md and the related ADR updates in 10-architecture-decisions.md because those were the largest corrections from the original knowledge base.

<!-- Phase 2: Domain Model & Data -->
- Review the relationship sections in 04-domain-model.md and data-model/data-model-erd.md closely, especially the distinction between real foreign keys and logical links, because that is the biggest correction from the original model docs.

<!-- Phase 3: Strategy & Execution -->
- Review 13-strategy-config-schema.md, 15-grid-controller.md, and 16-signal-contracts.md together because the biggest corrections were around entry-mode normalization and the fact that stop-loss exits still emit TakeProfit signals.
- Review the drawdown and high-water-mark sections in 01-trading-strategy.md, 14-strategy-runtime-model.md, and 19-scheduling-architecture.md because those were the largest runtime-behavior updates from the original docs.

<!-- Phase 4: Exchange, AI & Backtesting -->
- Review 18-backtesting-architecture.md and 24-backtesting-grid-engine-explained.md together because the biggest corrections were the async queue model, drawdown-aware risk enforcement, and regime gating.
- Review 33-risk-management-and-trade-sizing.md closely because the previous file mixed implemented and aspirational content and is now intentionally current-state only.

<!-- Phase 5: Frontend & UI -->
- Review 07-ui-design.md closely for the route table and strategy-authoring sections because those were the biggest corrections from the earlier UI description.
- Review 11-angular-instructions.md for the token table, auth storage note, and deployment-config wording because those capture the most important current-state conventions and deviations.

<!-- Phase 6: Operations, Business & Control Plane -->
- Review 29-control-plane-agent-architecture.md closely because it contains the largest substantive rewrite in this phase.
- Review 30-worker-execution-pipeline.md for the hosted-service topology, trigger-order cleanup, and drawdown-tier notes because those were the most code-sensitive corrections.
- Review 34-google-sso-authentication.md for the security scrub and config-binding notes.

<!-- Phase 7: Diagrams, README & New Knowledge Files -->
- Review the two diagram files closely because they were fully rewritten from the old single-process model to the current API and worker split architecture.
- Review 35-strategy-optimizer.md for scope accuracy, especially the signal-mode-only note and the walk-forward top-25 out-of-sample validation behavior.

## Release Summary

Completed the full seven-phase knowledge-base audit and refresh across .agent-context/0-knowledge. The final result aligns the documentation with the implemented codebase, removes stale aspirational content, adds missing optimizer coverage, rewrites the architecture diagrams for the current API and worker split model, and removes the plaintext Google OAuth client secret from the knowledge base.
