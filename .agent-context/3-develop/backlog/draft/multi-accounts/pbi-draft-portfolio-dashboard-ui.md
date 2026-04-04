# Portfolio Dashboard UI

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want a portfolio dashboard in the Angular UI that shows my sub-accounts, aggregated balances, exposure, and transfer history so that I can manage my multi-account setup visually.

## Problem Statement

With multiple sub-accounts, fund transfers, and portfolio-level exposure tracking available via API, the trader needs a unified dashboard to visualize and interact with these features. Without a UI, all multi-account management would require direct API calls.

## Requirements

### Functional Requirements

1. Portfolio overview panel: total equity, net/gross exposure per asset, capital allocation pie chart
2. Sub-accounts list: label, purpose, balance, active status, bound strategies count
3. Sub-account detail view: positions, open orders, balance history (from exchange)
4. Fund transfer form: select source/destination sub-account, enter amount, submit transfer
5. Transfer history table: timestamp, amount, source, destination, status
6. Hedge configuration panel: designate hedge account, set thresholds, enable/disable
7. Exposure visualization: bar or heatmap showing per-asset net exposure across sub-accounts

### Non-Functional Requirements

- Responsive layout consistent with existing Angular UI patterns
- Auto-refresh portfolio data on a configurable interval
- Loading states and error handling for API calls
- Accessibility: keyboard navigable, screen reader friendly labels

## Acceptance Criteria

- [ ] **Given** the portfolio dashboard, **When** loaded, **Then** total equity and per-asset exposure are displayed with data from the Portfolio API
- [ ] **Given** the sub-accounts list, **When** a user clicks a sub-account, **Then** the detail view shows that account's positions and orders
- [ ] **Given** the transfer form, **When** a valid transfer is submitted, **Then** the transfer appears in the history table with "Pending" status and updates when confirmed
- [ ] **Given** the exposure visualization, **When** positions change, **Then** the display refreshes to reflect current net exposure
- [ ] **Given** the hedge configuration panel, **When** the user sets a threshold and enables hedging, **Then** the configuration is persisted via the API

### Release Notes Information

- **Heading**: Portfolio Dashboard
- **Release note type**: Feature
- **Release Note Summary**: New portfolio dashboard providing aggregated visibility across all sub-accounts, with fund transfer controls and exposure visualization.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### Angular Components

- `PortfolioDashboardComponent` — main container
- `SubAccountListComponent` — sub-account cards/table
- `SubAccountDetailComponent` — detail view
- `FundTransferFormComponent` — transfer dialog/form
- `TransferHistoryComponent` — transfer log table
- `ExposureChartComponent` — exposure visualization
- `HedgeConfigComponent` — hedge settings

### API Integration

- Consumes: `/api/portfolio`, `/api/sub-accounts`, `/api/transfers`, hedge config endpoints

### Dependencies

- PBI: Sub-Account Registration API
- PBI: Fund Transfer Engine
- PBI: Portfolio Exposure Tracking
- PBI: Hedge Orchestration (for hedge config panel)

## Out of Scope

- Mobile-specific layouts
- Real-time WebSocket updates for portfolio (polling is acceptable for v1)
- P&L attribution charts per strategy
