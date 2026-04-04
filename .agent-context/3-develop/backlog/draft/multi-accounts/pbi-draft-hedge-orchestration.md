# Cross-Account Hedge Orchestration

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want the platform to automatically detect when my portfolio net exposure exceeds a threshold and trigger a hedge on a designated hedging sub-account so that I can limit my directional risk without manual intervention.

## Problem Statement

Running multiple strategies across sub-accounts can lead to unintended net directional exposure. For example, two grid strategies might both accumulate long BTC positions. Without automated hedging, the trader bears compounded directional risk. The platform needs a Hedge Orchestrator that monitors portfolio-level exposure and can trigger offsetting positions on a dedicated hedging sub-account.

## Requirements

### Functional Requirements

1. User can designate one sub-account as the "hedge account" for their portfolio
2. Configurable exposure thresholds per asset (e.g., "hedge when net BTC exposure exceeds 0.5 BTC")
3. When a threshold is breached, the orchestrator generates an `OpenHedge` signal targeting the hedge sub-account
4. Hedge positions are managed as reduce-only where possible to minimize additional risk
5. User can configure hedge sizing: full offset (net to zero) or partial (reduce by X%)
6. Capital rebalancing: if the hedge account lacks sufficient margin, trigger a fund transfer from the Treasury Service before opening the hedge
7. Hedge orchestrator runs on a configurable interval (e.g., every candle close, every N minutes)

### Non-Functional Requirements

- Hedge decisions are logged with full reasoning (exposure snapshot, threshold, action taken)
- Orchestrator must be idempotent — re-evaluating the same state does not create duplicate hedges
- Hedge orchestrator is an optional feature — users can enable/disable per portfolio
- Must not interfere with running strategies on other sub-accounts

## Acceptance Criteria

- [ ] **Given** a portfolio with net BTC exposure of 0.6 and a threshold of 0.5, **When** the orchestrator evaluates, **Then** an `OpenHedge` signal is generated to short 0.1 BTC on the hedge account (full offset mode)
- [ ] **Given** a portfolio below the exposure threshold, **When** the orchestrator evaluates, **Then** no hedge action is taken
- [ ] **Given** the hedge account has insufficient margin for the hedge, **When** the orchestrator triggers, **Then** a fund transfer is requested from the Treasury Service before the hedge is placed
- [ ] **Given** an existing hedge that already offsets the exposure, **When** the orchestrator re-evaluates, **Then** no duplicate hedge is created
- [ ] **Given** a user who has disabled hedge orchestration, **When** their exposure exceeds thresholds, **Then** no automated action is taken
- [ ] **Given** a hedge evaluation, **When** complete, **Then** a log entry records the exposure snapshot, threshold comparison, and action taken (or reason for no action)

### Release Notes Information

- **Heading**: Automated Hedge Orchestration
- **Release note type**: Feature
- **Release Note Summary**: Portfolio-level hedge orchestration automatically detects excess directional exposure and opens offsetting positions on a dedicated hedge account.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Configuration

- `HedgeConfig`: SubAccountId (hedge account), per-asset thresholds, sizing mode (full/partial), evaluation interval, enabled flag

### Service

- `IHedgeOrchestrator`: Evaluates portfolio exposure, compares against thresholds, generates `OpenHedge` signals
- Depends on `IPortfolioEngine` for exposure data
- Depends on Treasury Service for capital rebalancing

### Signal Integration

- Uses existing `OpenHedge` signal contract from the signal architecture

### Dependencies

- PBI: Sub-Account Domain Model
- PBI: Portfolio Exposure Tracking
- PBI: Strategy-to-SubAccount Binding
- PBI: Fund Transfer Engine

## Out of Scope

- Dynamic threshold adjustment based on market conditions (future LLM integration)
- Multi-asset correlation-based hedging
- Hedge P&L reporting
