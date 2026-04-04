# Strategy-to-SubAccount Binding

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want each strategy to be bound to a specific sub-account so that strategies execute in isolation and cannot interfere with each other's positions or capital.

## Problem Statement

The current architecture assumes one strategy per user executing against a single account. With multiple sub-accounts, the platform needs to enforce a binding between a `Strategy` and a `SubAccount`, ensuring that the trading worker routes orders to the correct sub-account's wallet and that position/order queries are scoped correctly.

## Requirements

### Functional Requirements

1. Each `Strategy` must be assigned to exactly one `SubAccount` before it can be started
2. API endpoint to bind/rebind a strategy to a sub-account (only when strategy is not running)
3. The trading worker uses the bound sub-account's credentials when placing orders for a strategy
4. A sub-account can host multiple strategies (but each strategy can only be on one sub-account)
5. Prevent starting a strategy if its bound sub-account is inactive or has insufficient balance
6. When querying strategy performance (orders, fills, positions), results are scoped to the bound sub-account

### Non-Functional Requirements

- Binding validation runs at strategy start time, not just at bind time
- Backward compatible — existing strategies with no explicit binding default to the user's default sub-account

## Acceptance Criteria

- [ ] **Given** a strategy with no sub-account binding, **When** the user tries to start it, **Then** the start is rejected with a message to bind a sub-account first (unless a default exists)
- [ ] **Given** a strategy bound to sub-account A, **When** the strategy places an order, **Then** the order is signed with sub-account A's wallet credentials
- [ ] **Given** a running strategy, **When** the user tries to rebind it to a different sub-account, **Then** the rebind is rejected until the strategy is stopped
- [ ] **Given** a sub-account that is deactivated, **When** a strategy bound to it tries to start, **Then** the start is rejected with a clear error
- [ ] **Given** two strategies bound to different sub-accounts on the same asset, **When** both are running, **Then** each maintains independent positions on their respective sub-accounts

### Release Notes Information

- **Heading**: Strategy-to-Account Isolation
- **Release note type**: Feature
- **Release Note Summary**: Strategies are now bound to specific sub-accounts, ensuring isolated execution with independent positions and capital.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| PUT | `/api/strategies/{id}/sub-account` | Bind strategy to sub-account |
| GET | `/api/strategies/{id}/sub-account` | Get current binding |

### Domain Changes

- Add `SubAccountId` to `Strategy` entity
- Modify `HyperliquidSigner` resolution in the worker to use per-sub-account credentials

### Dependencies

- PBI: Sub-Account Domain Model
- PBI: Sub-Account Registration API

## Out of Scope

- Automatic sub-account creation when deploying a strategy
- Multi-sub-account strategies (one strategy spanning multiple accounts)
