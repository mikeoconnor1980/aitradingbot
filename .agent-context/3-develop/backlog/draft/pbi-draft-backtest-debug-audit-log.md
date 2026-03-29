# Backtest Debug/Audit Log

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-29T08:39:23Z

## User Story

As a developer, I want a debug/audit log available in the backtesting results so that I can trace every decision the grid engine made and verify the algorithm is correct.

## Problem Statement

The backtesting grid engine currently only exposes a trade log (entry/exit/PnL) and equity curve. There is no visibility into WHY each decision was made — which candle triggered the grid, what indicator state was, why TP/SL price was set where it was, which orders got cancelled. This makes it impossible to build trust in the algorithm before going live or to diagnose bad trades.

## Requirements

### Functional Requirements

1. **Audit log toggle on BacktestConfig** — A boolean `EnableAuditLog` flag (default: `true`) on `BacktestConfig` controls whether debug data is captured during a run. When disabled, no debug data is generated or persisted, and the backtest runs with zero logging overhead.
2. **Per-candle evaluation log** — For every 15m candle processed (including warmup candles), capture:
   - Timestamp (Unix ms)
   - Candle OHLCV values
   - Whether candle is in warmup phase (`IsWarmup` flag)
   - Full indicator snapshot (all current indicator values: EMA, RSI, etc.)
   - `SetupDetected` result (true/false)
   - Current grid lifecycle state (Inactive, Deploying, Active, Closing, Closed)
   - Current position state (Flat, Long with size/avgEntry)
   - Signals emitted (list of signal types, or empty)
   - Associated `GridCycleId` (nullable — null when no cycle is active)
3. **Order event log** — For every order lifecycle event, capture:
   - Timestamp (Unix ms)
   - Event type enum: `Placed`, `Filled`, `Cancelled`, `Replaced`
   - Order details: order ID, side (Buy/Sell), type (Limit/Market), price, size
   - For `Filled`: fill price, fee calculated, maker/taker designation
   - For `Cancelled`: cancellation reason enum (`PositionOpened`, `GridRedeployed`, `StopLossTriggered`, `ManualCancel`)
   - Associated `GridCycleId`
4. **Grid cycle log** — For every completed grid cycle, capture:
   - `GridCycleId`
   - Deploy timestamp
   - Anchor price
   - Grid levels placed (count and price list)
   - Levels filled (count)
   - TP target price
   - SL trigger price
   - Exit reason (`TakeProfit`, `StopLoss`)
   - Cycle PnL (realised)
   - Cycle duration (deploy to close)
5. **Persistence as JSON blobs** — Debug data is persisted as 3 JSON blob columns on the `BacktestRun` entity: `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson`. Columns are nullable (null when audit log is disabled or for pre-existing runs).
6. **Dedicated API endpoint for debug data per cycle** — `GET /api/backtests/{id}/debug?cycleId={cycleId}` returns the 3 log types filtered to a specific grid cycle. Returns 404 if the backtest run does not exist, or 204 if no debug data is available (audit log was disabled or pre-existing run).
7. **UI: Expandable trade log rows** — Each row in the existing trade log table becomes expandable. On expand, lazy-load the debug data for that grid cycle via the new API endpoint. The expanded view shows:
   - Grid cycle summary (anchor, levels, exit reason, PnL, duration)
   - Order events timeline (placed → filled/cancelled, with reasons)
   - Per-candle evaluations for the cycle period (indicator snapshot, setup detected, grid state)
8. **Basic filtering in expanded view** — Filter controls within the expanded row to filter by: signal type emitted, `SetupDetected` value (true/false only).
9. **Color-coding by event type** — Order events are color-coded by type: green for fills, red for cancellations, blue for placements, orange for replacements.
10. **Export debug log** — A download button on the expanded view to export the debug data for that cycle as JSON or CSV.
11. **Graceful handling for pre-existing runs** — Trade log rows for backtests without debug data show the expand affordance as disabled with a tooltip explaining "Debug data not available for this run."

### Non-Functional Requirements

- **Performance**: Enabling the audit log should not increase backtest runtime by more than 20% compared to audit-disabled runs.
- **Storage**: Debug data for a 6-month backtest (~17,000 candle entries) must serialize and persist without error in SQLite.
- **API response time**: The debug endpoint should return filtered cycle data within 2 seconds for a typical run.
- **Backward compatibility**: Existing backtest runs must continue to load and display correctly (no migration-breaking changes).

## Acceptance Criteria

- [ ] **Given** a backtest config with `EnableAuditLog = true`, **When** the backtest completes, **Then** the result includes populated `CandleLogJson`, `OrderEventLogJson`, and `GridCycleLogJson` columns in the database.
- [ ] **Given** a backtest config with `EnableAuditLog = false`, **When** the backtest completes, **Then** the debug JSON columns are null and no debug data is captured during the run.
- [ ] **Given** a completed backtest with audit data, **When** the per-candle evaluation log is inspected, **Then** every 15m candle (including warmup) has an entry with timestamp, OHLCV, full indicator snapshot, SetupDetected result, grid lifecycle state, position state, signals emitted, and GridCycleId.
- [ ] **Given** a warmup candle entry in the log, **When** inspected, **Then** it has `IsWarmup = true` and no signals emitted.
- [ ] **Given** a grid cycle where buy orders are placed and some are cancelled, **When** the order event log is inspected, **Then** each cancellation has an enum reason code (e.g., `PositionOpened`).
- [ ] **Given** a completed grid cycle, **When** the grid cycle log is inspected, **Then** it contains cycle ID, deploy time, anchor price, levels placed, fill count, TP/SL prices, exit reason, cycle PnL, and duration.
- [ ] **Given** a backtest ID and a grid cycle ID, **When** `GET /api/backtests/{id}/debug?cycleId={cycleId}` is called, **Then** it returns the 3 log types filtered to that cycle.
- [ ] **Given** a backtest without audit data, **When** the debug endpoint is called, **Then** it returns 204 No Content.
- [ ] **Given** the trade log table in the UI, **When** a row for a run with audit data is clicked, **Then** it expands to show the grid cycle summary, order events, and candle evaluations for that cycle, loaded via the debug API.
- [ ] **Given** the expanded debug view, **When** the user applies a filter (signal type or SetupDetected), **Then** only matching candle evaluation rows are displayed.
- [ ] **Given** the expanded debug view, **When** the user clicks the JSON export button, **Then** the debug data for that cycle downloads as a `.json` file.
- [ ] **Given** the expanded debug view, **When** the user clicks the CSV export button, **Then** the debug data for that cycle downloads as a `.csv` file.
- [ ] **Given** order events in the expanded view, **When** displayed, **Then** they are color-coded: green (fills), red (cancels), blue (placements), orange (replacements).
- [ ] **Given** a pre-existing backtest run without debug data, **When** the trade log is displayed, **Then** the expand control is disabled with a tooltip "Debug data not available for this run."
- [ ] **Given** audit logging is enabled, **When** a backtest completes, **Then** the total runtime is no more than 20% slower than with audit logging disabled (for the same config and data).

### Release Notes Information

- **Heading**: Backtest Debug/Audit Log
- **Release note type**: Feature
- **Release Note Summary**: Backtesting results now include a comprehensive debug/audit log that traces every decision the grid engine made — per-candle evaluations with indicator snapshots, order lifecycle events with cancellation reasons, and grid cycle summaries. Expand any trade in the results to inspect why each decision was made.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/backtests/{id}/debug?cycleId={cycleId}` | Returns debug data (candle evals, order events, grid cycle summary) filtered to a specific grid cycle. 404 if run not found, 204 if no debug data. |

### Database Changes

- 3 new nullable `TEXT` columns on `BacktestRun`: `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson`
- 1 new `bool` column: `AuditLogEnabled`

### Integration Events

None — this is a read-only diagnostic feature within the backtesting subsystem.

### Jobs

None.

## Out of Scope

- Linking debug data points to the equity chart (click-to-inspect on chart)
- Real-time debug streaming during a running backtest
- Debug data for live trading (this is backtest-only)
- Full-text search across debug logs
- Debug data comparison between two backtest runs
- Per-candle evaluation log for non-15m timeframes (only 15m trigger candles are logged)
- Detailed technical design, file changes, class diagrams, or implementation architecture
