# Portfolio Exposure Tracking

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want to see my aggregated portfolio exposure across all sub-accounts so that I understand my total risk and capital allocation at a glance.

## Problem Statement

When running multiple strategies on separate sub-accounts, the trader loses visibility into their total exposure. A long position on one sub-account may be partially offset by a short on another, but without aggregation, the trader cannot see their net position or total capital at risk. The platform needs a Portfolio Engine that aggregates balances, positions, and exposure metrics across all sub-accounts.

## Requirements

### Functional Requirements

1. Aggregate total equity across all sub-accounts for a user
2. Calculate net exposure per asset across all sub-accounts (sum of long and short positions)
3. Calculate gross exposure per asset (absolute sum of all positions regardless of direction)
4. Track capital allocation: how much of total equity is deployed per sub-account
5. Provide a portfolio snapshot API that returns all aggregated metrics in a single call
6. Support real-time balance refresh by querying Hyperliquid for each sub-account's `clearinghouseState`

### Non-Functional Requirements

- Portfolio calculations must handle stale data gracefully (show last-known with timestamp)
- Aggregation logic is purely read-side — no write-side state machine needed
- All data tenant-scoped by UserId

## Acceptance Criteria

- [ ] **Given** a user with 3 sub-accounts holding positions in BTC, **When** the portfolio snapshot is requested, **Then** the net BTC exposure is the algebraic sum of all BTC positions across sub-accounts
- [ ] **Given** sub-accounts with balances of 1000, 2000, and 500 USDC, **When** the portfolio snapshot is requested, **Then** total equity shows 3500 USDC
- [ ] **Given** one sub-account long 0.5 BTC and another short 0.3 BTC, **When** viewing net exposure, **Then** net exposure shows +0.2 BTC and gross exposure shows 0.8 BTC
- [ ] **Given** a sub-account that is unreachable, **When** the portfolio snapshot is requested, **Then** the snapshot includes that sub-account's last-known data with a stale timestamp warning
- [ ] **Given** the portfolio snapshot, **When** examining capital allocation, **Then** each sub-account shows its equity as a percentage of total portfolio equity

### Release Notes Information

- **Heading**: Portfolio Exposure Tracking
- **Release note type**: Feature
- **Release Note Summary**: Aggregated portfolio view showing total equity, net/gross exposure per asset, and capital allocation across all sub-accounts.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/portfolio` | Get aggregated portfolio snapshot |
| GET | `/api/portfolio/exposure` | Get per-asset net and gross exposure |
| GET | `/api/portfolio/allocation` | Get capital allocation breakdown |

### Service

- `IPortfolioEngine`: Aggregates data from sub-accounts, calculates exposure metrics
- Uses `IHyperliquidRestClient.PostInfoAsync` with `clearinghouseState` per sub-account wallet

### Dependencies

- PBI: Sub-Account Domain Model
- PBI: Sub-Account Registration API

## Out of Scope

- Historical portfolio tracking / time series
- P&L attribution per strategy
- Automated rebalancing triggers (part of Hedge Orchestration PBI)
