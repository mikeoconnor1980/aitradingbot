# Ideas Log

> Checklist of ideas, improvements, and bugs. Tick off when done.
> Link to the PBI in `3-develop/backlog/draft/` once one exists.

## Big Ideas

| Done | Idea | Date | PBI |
|------|------|------|-----|
| [ ] | Trade journalling - want to track trades made, whether won or lost, with key metrics, like profit/loss, strategy employed, entry, exit, reason for entry and exit etc - to be defined | 9 Apr 2026 | — |
| [ ] | Scenario scanner - looking for specific situations across multiple assets and apllying given strategy, for example find RSI bullish divergence when price above EMA, or pullback to EMA in uptrend etc, find examples and hit trades | 9 Apr 2026 | — |
| [ ] | Tokenised stock trading - can trade large cap US stocks, long/short and with leverage - strategies could equally apply to these and broadens appeal outside of crypto | 9 Apr 2026 | — |
| [ ] | Tokenised stock trading - Dividend harvesting - scan stocks for ex-div dates, enter long trades if metrics support, harvest divs and possibly sell when able | 9 Apr 2026 | — |
| [ ] | Strategy trainer — explain what is required in natural language, upload screenshots of charts alongside, have Ai analyse what metrics are needed, determine what calcs are needed, if a new set not known then create and allow config, set out the trading logic | 9 Apr 2026 | — |
| [ ] | Strategy selection — multiple types available for backtesting (Recovery Grid, DCA, Trend, etc.) | 30 Mar 2026 | — |
| [ ] | Save and name strategies per user account | 30 Mar 2026 | — |
| [ ] | Execute a saved strategy (live trading) | 30 Mar 2026 | — |
| [ ] | Multiple Hyperliquid accounts on dashboard | 30 Mar 2026 | — |
| [ ] | User account can have multiple wallets across multiple exchanges | 30 Mar 2026 | — |
| [ ] | Select account for trade executions | 30 Mar 2026 | — |
| [ ] | Rebalance between accounts / to a target ratio | 30 Mar 2026 | — |
| [ ] | Pine Script (TV script) parser, chart indicator draw and signal execution (additional strategies) | 31 Mar 2026 | — |

## Improvements

| Done | Idea | Date | PBI |
|------|------|------|-----|
| [ ] | Develop risk management functionality | 9 Apr 2026 | — |
| [ ] | Allow increase grid size vertically in market data tab | 3 Apr 2026 | — |
| [ ] | Timeframe box on market data tab should be at top of price chart - i.e. raise higher up page | 3 Apr 2026 | — |
| [ ] | Good to have a checkbox to show/hide trade entry/exit labels on chart (can clutter) | 3 Apr 2026 | — |
| [x] | Show trades on main chart | 30 Mar 2026 | [pbi-draft-show-trades-on-chart](../3-develop/backlog/draft/pbi-draft-show-trades-on-chart.md) |
| [ ] | Show indicators on main chart | 30 Mar 2026 | — |
| [ ] | Backtest run deletion / archival | 30 Mar 2026 | — |
| [ ] | Trigger backtest data sync from the UI | 30 Mar 2026 | — |
| [ ] | Update backtest data since last entry (incremental sync) | 30 Mar 2026 | — |
| [ ] | Automatic hourly job to append new backtest data | 30 Mar 2026 | — |
| [ ] | Export trades to CSV | 30 Mar 2026 | — |
| [ ] | All times displayed in UTC | 30 Mar 2026 | — |
| [ ] | Change all dates throughout the project to use `DD Month YYYY - HH:MM` format | 29 Mar 2026 | [pbi-draft-date-format-standardization](../3-develop/backlog/draft/pbi-draft-date-format-standardization.md) |
| [ ] | Dashboard: show liquidation price in the grid | 29 Mar 2026 | [pbi-draft-dashboard-liquidation-price](../3-develop/backlog/draft/pbi-draft-dashboard-liquidation-price.md) |
| [x] | SL/TP modal: display liquidation price and live asset price | 29 Mar 2026 | [pbi-draft-sltp-modal-liquidation-live-price](../3-develop/backlog/draft/pbi-draft-sltp-modal-liquidation-live-price.md) |
| [ ] | Dashboard: add activity date-range filter | 29 Mar 2026 | [pbi-draft-dashboard-activity-date-range](../3-develop/backlog/draft/pbi-draft-dashboard-activity-date-range.md) |
| [x] | Remove connection bubble from header; move functionality to the connection pill on right | 29 Mar 2026 | [pbi-draft-connection-header-consolidation](../3-develop/backlog/draft/pbi-draft-connection-header-consolidation.md) |

## Bugs / Issues

| Done | Issue | Date | PBI |
|------|-------|------|-----|
| [ ] | Backtest run not saving into Past Results tab | 29 Mar 2026 | [pbi-draft-bug-backtest-past-results-save](../3-develop/backlog/draft/pbi-draft-bug-backtest-past-results-save.md) |
| [ ] | Selecting more than 2 past results flicks to a different tab — if 2 is the max, show a message | 29 Mar 2026 | [pbi-draft-bug-past-results-selection-limit](../3-develop/backlog/draft/pbi-draft-bug-past-results-selection-limit.md) |