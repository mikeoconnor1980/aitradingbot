# PBI Specification: F5 — Backtest UI Dashboard

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-27T23:08:35Z
**PRD:** [candle-persistence-backtesting-prd.md](../../prd/candle-persistence-backtesting-prd.md)
**Implementation Phase:** 5
**Risk Level:** Low
**Depends On:** F4 (Backtest API & Results)

---

## Summary

Build an Angular dashboard for triggering backtests, viewing results, and comparing past runs. The UI lives at `/backtesting` in the existing Angular app and consumes the F4 API endpoints. It includes a configuration form to run backtests, a results view with summary metrics and equity curve chart, a trade log table, a past results list, and a side-by-side comparison mode for two backtest runs.

### User Story

> As an **Operator**, I want to **configure and run backtests from a web UI, view detailed results with an equity curve chart, and compare two runs side-by-side** so that **I can visually evaluate and iterate on strategy parameters without using curl or Postman**.

### Business Value

Provides the primary interface for strategy validation and iteration. Visual feedback (equity curves, trade markers, metrics comparison) enables faster parameter tuning than raw JSON output. Persisted result browsing and "re-run with changes" capability make strategy iteration efficient. Side-by-side comparison directly supports the core use case of parameter variation testing.

---

## Problem Statement

The F4 API exposes backtest functionality via REST endpoints, but the only way to interact with it is through direct HTTP calls (curl, Postman). The operator needs a visual interface to configure backtests, see results at a glance (equity curve, key metrics), browse past runs, and compare parameter variations — all from the existing Angular app.

---

## Requirements

### Functional Requirements

#### Navigation & Routing

- [ ] A `/backtesting` route is added to the Angular app
- [ ] A "Backtesting" nav link appears in the app header alongside Dashboard, Connection, Market Data, and Order Entry

#### Run Backtest Form

- [ ] A form allows the operator to configure and trigger a backtest
- [ ] Form fields: symbol selector (dropdown), date range picker (start/end dates), interval checkboxes (15m, 1h, 4h), and all strategy config parameters: gridLevels, gridSpacing, takeProfitPercent, breakdownThreshold, makerFee, takerFee, slippage, positionSize, leverage, stopLossPercent
- [ ] The form has a "Validate Data" button that calls `GET /api/backtests/validate` and displays a coverage report showing available date ranges and candle counts per interval
- [ ] The coverage report indicates whether the selected date range is fully covered, partially covered, or has no data
- [ ] The form has a "Run Backtest" button that calls `POST /api/backtests` with the configured parameters
- [ ] While the backtest is running, the "Run Backtest" button shows a loading/spinner state and is disabled
- [ ] Form validation enforces the same rules as the API (gridLevels > 0, leverage > 0, startDate before endDate, etc.) with inline error messages
- [ ] Default values are pre-populated for strategy config fields (matching Hyperliquid standard fees, sensible grid defaults)

#### Results Summary View

- [ ] After a backtest completes, the results are displayed below the form (or in a results tab)
- [ ] Key metrics are shown as summary cards: Total PnL, Win Rate (%), Max Drawdown, Total Trades, Winning Trades, Losing Trades, Average Trade PnL, Average Hold Time, Hedges Opened, Total Fees Paid
- [ ] An equity curve line chart shows equity over time using the lightweight-charts (TradingView) library
- [ ] Trade entry/exit markers are plotted on the equity chart (e.g., up arrow for entry, down arrow for exit, coloured by PnL positive/negative)
- [ ] The strategy config used for the run is displayed for reference

#### Trade Log Table

- [ ] A sortable, filterable table shows all trades from the backtest
- [ ] Columns: Entry Time, Exit Time, Entry Price, Exit Price, Side, Size, PnL, Fees
- [ ] PnL values are colour-coded (green for positive, red for negative)
- [ ] The table supports sorting by any column

#### Past Results List

- [ ] A paginated list shows previously run backtests
- [ ] Each row shows: run date, symbol, date range, total trades, win rate, total PnL, max drawdown
- [ ] Clicking a row navigates to the full result detail view for that backtest
- [ ] A "Re-run with changes" button on a past result pre-fills the run form with that result's strategy config and date range, allowing the operator to tweak parameters and re-run
- [ ] This requires a new `GET /api/backtests` list endpoint (paginated) to be added to the API (scoped to this PBI)

#### Side-by-Side Comparison

- [ ] The operator can select two backtest results to compare
- [ ] A comparison view shows a metrics table with both runs' values side-by-side, with delta/difference column
- [ ] The equity curves of both runs are overlaid on the same chart (different colours) using lightweight-charts
- [ ] Values that are better/worse are highlighted (e.g., higher PnL = green, deeper drawdown = red)

#### API List Endpoint (New)

- [ ] A `GET /api/backtests` endpoint is added to the API that returns a paginated list of past backtest runs
- [ ] Supports query parameters: `page` (default: 1), `pageSize` (default: 20)
- [ ] Returns summary data for each run (not the full trade log) — id, symbol, intervals, startDate, endDate, totalTrades, winRate, totalPnl, maxDrawdown, createdAt
- [ ] Returns pagination metadata: total count, page, pageSize, total pages

#### Error Handling

- [ ] API validation errors (400) are displayed as inline form errors mapped to the relevant fields
- [ ] "No candle data" errors (404) show a user-friendly message with a suggestion to check coverage first
- [ ] Timeout errors (408) show a message suggesting a shorter date range
- [ ] Network errors show a generic error banner with retry option
- [ ] If the backtest returns no trades, the results view shows an empty state message (e.g., "The strategy did not generate any trades in this date range")

### Non-Functional Requirements

- [ ] The charting library is `lightweight-charts` (TradingView open-source)
- [ ] The UI uses Angular Material components consistent with the existing app (mat-tab-group, mat-icon, mat-spinner, etc.)
- [ ] The form and results are responsive (usable on tablet-width screens)
- [ ] The equity chart renders within 1 second for up to 35K data points
- [ ] No authentication required (POC — single operator)

---

## User Flow

### Happy Path — Run a Backtest

1. Operator clicks "Backtesting" in the nav bar
2. The backtesting page loads with the run form and past results list
3. Operator selects symbol (BTC), sets date range (2024-01-01 to 2024-12-31), checks intervals (15m, 1h, 4h)
4. Operator clicks "Validate Data" — coverage report shows full coverage for all intervals
5. Operator configures strategy: gridLevels=10, gridSpacing=0.5, TP=1%, leverage=3x, etc.
6. Operator clicks "Run Backtest" — button shows spinner
7. After ~15 seconds, results appear: PnL card shows +$4,521, Win Rate 72.3%, Max Drawdown -$1,234
8. Equity curve chart shows growth over the year with entry/exit markers
9. Trade log table shows 847 trades with sortable columns
10. The run appears at the top of the past results list

### Happy Path — Compare Two Runs

1. Operator runs a backtest with gridLevels=10
2. Operator clicks "Re-run with changes" on the result
3. Form pre-fills with the same config; operator changes gridLevels to 15
4. Operator runs the second backtest
5. Operator selects both results from the past results list
6. Comparison view shows metrics side-by-side with delta column
7. Equity curves overlay on the same chart in different colours

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Invalid form values (e.g., gridLevels = 0) | Inline validation error on the field, "Run Backtest" button disabled |
| No candle data for date range | Error banner: "No candle data found. Use Validate Data to check coverage." |
| Backtest times out (408) | Error banner: "Backtest timed out. Try a shorter date range." |
| Network error / API unreachable | Error banner: "Unable to reach API. Check connection." with retry button |
| Backtest returns zero trades | Empty state: "Strategy did not generate any trades in this date range" |
| Past results list is empty | Empty state: "No backtests run yet. Configure and run your first backtest above." |

---

## Technical Considerations

### API Endpoints Consumed

| Method | Route | Usage |
|--------|-------|-------|
| POST | `/api/backtests` | Trigger a backtest run |
| GET | `/api/backtests/{id}` | Retrieve a single result (detail view) |
| GET | `/api/backtests/validate` | Validate candle data coverage |
| GET | `/api/backtests` | **New** — Paginated list of past backtest runs |

### Key Components

| Component | Location | Description |
|-----------|----------|-------------|
| `BacktestPageComponent` | `features/backtesting/` | Page-level component with tabs (Run / Results / Compare) |
| `BacktestFormComponent` | `features/backtesting/backtest-form/` | Configuration form with validation |
| `BacktestResultComponent` | `features/backtesting/backtest-result/` | Summary metrics cards + equity chart + trade log |
| `BacktestListComponent` | `features/backtesting/backtest-list/` | Paginated list of past results |
| `BacktestCompareComponent` | `features/backtesting/backtest-compare/` | Side-by-side metrics + overlaid equity curves |
| `CoverageReportComponent` | `features/backtesting/coverage-report/` | Coverage validation display |
| `EquityChartComponent` | `features/backtesting/equity-chart/` | Lightweight-charts wrapper for equity curve |
| `TradeLogTableComponent` | `features/backtesting/trade-log-table/` | Sortable trade log table |
| `BacktestService` | `core/services/` | HTTP service for all backtest API calls |

### Charting Library

- **Library:** `lightweight-charts` (TradingView, MIT license)
- **npm:** `lightweight-charts`
- **Usage:** Line chart for equity curve, markers API for trade entry/exit points
- **Comparison:** Two line series on the same chart with different colours

### New API Endpoint — `GET /api/backtests`

```
GET /api/backtests?page=1&pageSize=20

Response:
{
  "items": [
    {
      "id": "guid",
      "symbol": "BTC",
      "intervals": ["15m", "1h", "4h"],
      "startDate": "2024-01-01T00:00:00Z",
      "endDate": "2024-12-31T23:59:59Z",
      "totalTrades": 847,
      "winRate": 72.3,
      "totalPnl": 4521.87,
      "maxDrawdown": -1234.56,
      "createdAt": "2026-03-27T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 5,
  "totalPages": 1
}
```

---

## Out of Scope

- Real-time progress bar during backtest execution (SignalR/WebSocket)
- CSV/Excel export of results or trade log
- Editing or deleting past backtest runs
- Batch/sweep: running multiple configs in one submission
- Multi-asset comparison (comparing BTC vs ETH runs)
- Authentication/authorization (POC phase)
- Mobile-optimized layout (tablet-width minimum)
- Print-friendly view of results

---

## Acceptance Criteria

- [ ] **Given** the Angular app is running, **When** the operator navigates to `/backtesting`, **Then** the backtesting page loads with the run form and past results list
- [ ] **Given** the backtesting page, **When** the operator views the nav bar, **Then** a "Backtesting" link is visible alongside the other nav items
- [ ] **Given** valid form values, **When** the operator clicks "Run Backtest", **Then** the button shows a loading state and the backtest runs via `POST /api/backtests`
- [ ] **Given** a backtest completes successfully, **Then** summary metric cards are displayed showing Total PnL, Win Rate, Max Drawdown, Total Trades, and other metrics
- [ ] **Given** a backtest completes, **Then** an equity curve chart is rendered using lightweight-charts showing equity over time
- [ ] **Given** a backtest completes with trades, **Then** trade entry/exit markers are plotted on the equity chart
- [ ] **Given** a backtest completes, **Then** a sortable trade log table is displayed with Entry Time, Exit Time, Entry Price, Exit Price, Side, Size, PnL, and Fees columns
- [ ] **Given** a backtest returns zero trades, **Then** an empty state message is shown: "Strategy did not generate any trades in this date range"
- [ ] **Given** the operator clicks "Validate Data", **Then** the coverage report shows available date ranges and candle counts per interval
- [ ] **Given** invalid form values (e.g., gridLevels = 0), **Then** inline validation errors are shown and the Run Backtest button is disabled
- [ ] **Given** the API returns a validation error (400), **Then** the error is displayed as inline form errors on the relevant fields
- [ ] **Given** the API returns a timeout (408), **Then** an error message suggests trying a shorter date range
- [ ] **Given** past backtest runs exist, **Then** the past results list shows a paginated table of previous runs with key metrics
- [ ] **Given** the operator clicks a past result, **Then** the full result detail is displayed including metrics, equity chart, and trade log
- [ ] **Given** the operator clicks "Re-run with changes" on a past result, **Then** the run form is pre-filled with that result's strategy config and date range
- [ ] **Given** the operator selects two results for comparison, **Then** a side-by-side metrics table is shown with delta values
- [ ] **Given** two results are being compared, **Then** their equity curves are overlaid on the same chart in different colours
- [ ] **Given** `GET /api/backtests?page=1&pageSize=20` is called, **Then** a paginated list of backtest summaries is returned (without full trade logs)

### Release Notes Information

- **Heading**: Backtest Dashboard UI
- **Release note type**: Feature
- **Release Note Summary**: Angular dashboard for running backtests, viewing results with equity curve charts and trade logs, browsing past runs, and comparing two backtest results side-by-side with overlaid equity curves.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F3** — Backtest Replay Engine provides the core execution logic
- **F4** — Backtest API & Results provides the REST endpoints consumed by this UI
- **F1** — Candle Data Persistence provides the underlying data validated via the coverage endpoint
