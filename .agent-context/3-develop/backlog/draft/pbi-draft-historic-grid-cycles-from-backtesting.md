# Populate Historic Grid Cycles from Backtesting Data

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-28T00:00:00Z

## User Story

As a trader/subscriber, I want to see historical grid cycle performance derived from backtesting data (going back to 2019 for BTC), so that I can understand how the grid strategy would have performed across different market conditions before risking real capital.

## Problem Statement

The backtesting engine can run the full strategy pipeline against historical candle data (BTC back to 2019) and already tracks GridCycleId per trade and counts completed grid cycles. However, there is no GridCycle domain entity, no persistence, no API endpoints, and no UI to view grid cycle history. GridState is ephemeral and in-memory only. The frontend BacktestTrade model is also missing the GridCycleId field (serialization gap). Traders have no way to review how the grid strategy performed across historical market conditions at a per-cycle granularity.

## Requirements

### Functional Requirements

1. Create a `GridCycle` domain entity that captures completed grid lifecycle data including: entry time, exit time, symbol, grid levels, number of fills, PnL, fees, hedge activity, duration, and trade count
2. Add an `Origin` enum field on GridCycle (`Backtest` vs `Live`) so backtested grid cycles are clearly distinguished from real trading
3. Persist GridCycle records to the database (new DbSet, EF migration)
4. Create a service/command to extract completed grid cycles from BacktestResult.TradeLog by grouping trades by GridCycleId and persisting them as GridCycle records with Origin=Backtest
5. Create API endpoints:
   - `GET /api/grid-cycles` — list grid cycles with filters for symbol, date range, origin, and pagination
   - `GET /api/grid-cycles/{id}` — retrieve a single grid cycle with its associated trades
6. Create Angular components to display grid history with clear visual distinction between backtested and live cycles
7. Fix the frontend BacktestTrade model to include GridCycleId (close the serialization gap)
8. Default display: show backtested data for history older than 30 days, live data for recent — allow user filtering/toggling
9. GridCycle records must be tenant-scoped by UserId consistent with multi-tenant architecture

### Non-Functional Requirements

- TBD (to be gathered during interview)

## Acceptance Criteria

Use BDD Given-When-Then format:

- [ ] **Given** a completed backtest run with trades containing GridCycleId values, **When** the backtest completes (or an import is triggered), **Then** GridCycle records are persisted to the database with Origin=Backtest
- [ ] **Given** persisted grid cycles exist, **When** a user calls `GET /api/grid-cycles` with optional filters (symbol, origin, date range), **Then** the API returns a paginated list of matching grid cycles
- [ ] **Given** a specific grid cycle exists, **When** a user calls `GET /api/grid-cycles/{id}`, **Then** the API returns the grid cycle details including associated trades
- [ ] **Given** grid cycle records exist in the database, **When** a user navigates to the grid history UI, **Then** a table displays: symbol, start date, end date, grid levels, total PnL, fees, trades count, win rate, duration, and origin badge
- [ ] **Given** both backtested and live grid cycles exist, **When** viewing the grid history table, **Then** backtested cycles are visually distinct from live cycles (badge/color/icon)
- [ ] **Given** a backtest trade with a GridCycleId on the backend, **When** the trade is serialized to the frontend, **Then** the frontend BacktestTrade model includes the GridCycleId field
- [ ] **Given** BTC is the primary dataset, **When** the system architecture is reviewed, **Then** the GridCycle entity and endpoints support multi-token filtering

### Release Notes Information

- **Heading**: Historical Grid Cycle Performance from Backtesting
- **Release note type**: Feature
- **Release Note Summary**: View historical grid strategy performance data derived from backtesting (BTC data back to 2019). Grid cycles show per-cycle PnL, duration, fills, and fees with clear visual distinction between backtested and live results.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### API Endpoints (if relevant)

- `GET /api/grid-cycles` — Query params: symbol, origin (Backtest|Live), startDate, endDate, page, pageSize. Returns paginated GridCycleSummaryDto list.
- `GET /api/grid-cycles/{id}` — Returns GridCycleDetailDto including associated trades.

### Integration Events (if relevant)

TBD

### Jobs (if relevant)

TBD

## Out of Scope

- Live grid cycle persistence (separate PBI — triggered when GridController transitions to Closed state)
- Advanced grid analytics/charts (future enhancement: cycle duration distribution, PnL by market regime)
- Automatic re-backtesting when strategy config changes
