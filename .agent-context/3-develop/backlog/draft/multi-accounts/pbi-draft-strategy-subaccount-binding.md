# Strategy-to-SubAccount Binding

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z
**Last Refined:** 2026-04-04T17:38:57Z

## User Story

As a **trader**, I want each strategy to be bound to a specific sub-account so that strategies execute in isolation and cannot interfere with each other's positions or capital.

## Problem Statement

The current architecture assumes one strategy per user executing against a single account. The `Strategy` entity has no `SubAccountId` property, and `IHyperliquidSigner` is registered as a singleton — meaning all strategies share the same wallet credentials. With multiple sub-accounts, the platform needs to enforce a binding between a `Strategy` and a `SubAccount`, ensuring the trading worker resolves the correct signer per-strategy and that position/order queries are scoped to the bound sub-account.

## Requirements

### Functional Requirements

1. Add a nullable `SubAccountId` (Guid?) property to the `Strategy` entity
2. Dedicated API endpoint to bind, rebind, or unbind a strategy to/from a sub-account (only when strategy is not running)
3. Dedicated API endpoint to query the current binding for a strategy
4. At strategy start time, validate all three conditions as a single gate: binding exists, sub-account is active, sub-account has non-zero balance (equity > 0)
5. The trading worker resolves an `IHyperliquidSigner` per-strategy using the bound sub-account's wallet credentials (replacing the current singleton pattern)
6. A sub-account can host multiple strategies with no limit (each strategy can only be bound to one sub-account at a time)
7. When querying strategy performance (orders, fills, positions), results are scoped to the bound sub-account
8. When a sub-account is deactivated (via PBI-02), any running strategies bound to it are force-stopped: open orders are cancelled on the exchange, then the strategy is stopped locally
9. Existing strategies are auto-bound to the user's default sub-account via data migration (backward compatible with PBI-01's auto-created default sub-account)
10. When rebinding a stopped strategy to a different sub-account, historical data (orders, fills, positions) stays linked to the old sub-account — rebinding only affects future execution

### Non-Functional Requirements

- Binding validation runs at strategy start time, not just at bind time (sub-account status may change between bind and start)
- Backward compatible — the migration auto-binds existing strategies so no user action is required
- Force-stop on sub-account deactivation must cancel exchange orders before marking the strategy as stopped (clean shutdown)

## Acceptance Criteria

- [ ] **Given** a strategy with no sub-account binding (SubAccountId is null), **When** the user tries to start it, **Then** the start is rejected with a message to bind a sub-account first
- [ ] **Given** a strategy bound to sub-account A, **When** the strategy places an order, **Then** the order is signed with sub-account A's wallet credentials (per-strategy signer resolution)
- [ ] **Given** a running strategy, **When** the user tries to bind, rebind, or unbind its sub-account, **Then** the request is rejected with HTTP 409 until the strategy is stopped
- [ ] **Given** a sub-account that is inactive, **When** a strategy bound to it tries to start, **Then** the start is rejected with a clear error indicating the sub-account is inactive
- [ ] **Given** a sub-account with zero balance, **When** a strategy bound to it tries to start, **Then** the start is rejected with a clear error indicating insufficient balance
- [ ] **Given** two strategies bound to different sub-accounts on the same asset, **When** both are running, **Then** each maintains independent positions on their respective sub-accounts
- [ ] **Given** a running strategy bound to sub-account A, **When** sub-account A is deactivated, **Then** the system cancels the strategy's open orders on the exchange and stops the strategy
- [ ] **Given** a strategy previously bound to sub-account A with historical orders, **When** the strategy is rebound to sub-account B, **Then** historical orders remain linked to sub-account A and new orders use sub-account B
- [ ] **Given** a strategy bound to a sub-account, **When** the user unbinds it (clears SubAccountId), **Then** SubAccountId becomes null and the strategy cannot be started until rebound
- [ ] **Given** the database migration, **When** applied to an existing database, **Then** all existing strategies are auto-bound to the user's default sub-account

### Release Notes Information

- **Heading**: Strategy-to-Account Isolation
- **Release note type**: Feature
- **Release Note Summary**: Strategies are now bound to specific sub-accounts, ensuring isolated execution with independent positions and capital. Existing strategies are automatically bound to the default sub-account.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| PUT | `/api/strategies/{id}/sub-account` | Bind or rebind strategy to a sub-account |
| DELETE | `/api/strategies/{id}/sub-account` | Unbind strategy (clear SubAccountId) |
| GET | `/api/strategies/{id}/sub-account` | Get current binding |

### Domain Changes

- Add nullable `SubAccountId` (Guid?) to `Strategy` entity
- Add `BindSubAccount(Guid subAccountId)`, `UnbindSubAccount()` domain methods that enforce `IsRunning == false`
- Worker resolves `IHyperliquidSigner` per-strategy using the bound sub-account's credentials (replaces singleton registration)

### Sub-Account Deactivation Side Effect

- When PBI-02's deactivation endpoint is called, it must trigger force-stop for any running strategies on that sub-account
- Force-stop sequence: cancel open orders on Hyperliquid → set `IsRunning = false` → persist

### Database Migration

- Add nullable `SubAccountId` FK column to `Strategies` table
- Data migration: backfill `SubAccountId` with each user's default sub-account ID (from PBI-01 migration)

### Dependencies

- PBI-01: Sub-Account Domain Model (SubAccount entity, default sub-account creation)
- PBI-02: Sub-Account Registration API (deactivation endpoint that triggers force-stop)

## Out of Scope

- Automatic sub-account creation when deploying a strategy
- Multi-sub-account strategies (one strategy spanning multiple accounts)
- Balance-aware margin calculation (only non-zero equity check in this PBI)
- UI for strategy-to-sub-account binding (part of Portfolio Dashboard PBI)
