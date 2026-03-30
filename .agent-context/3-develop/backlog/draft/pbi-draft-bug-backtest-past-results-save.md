# Bug: Backtest Run Not Saving into Past Results Tab

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## Bug Summary

When a backtest run completes, the results are not being persisted to the Past Results tab. The user can see the results immediately after running, but they disappear and do not appear in the historical past results list.

## Steps to Reproduce

1. Navigate to the Backtesting page
2. Configure and run a backtest
3. Wait for the backtest to complete and view the results
4. Switch to the Past Results tab
5. **Expected:** The completed backtest run appears in the list
6. **Actual:** The run is not listed

## Requirements

### Functional Requirements

- [ ] After a backtest run completes, the results must be persisted (database or local storage as per current architecture)
- [ ] The Past Results tab must list all previously completed backtest runs
- [ ] Clicking a past result should load the full result data

### Investigation Notes

- Check whether the save call is being made after the run completes
- Check whether the backend endpoint for saving results is returning success
- Check whether the Past Results tab is querying the correct data source

## Acceptance Criteria

- [ ] **Given** a backtest run completes successfully, **When** the user navigates to the Past Results tab, **Then** the run appears in the list
- [ ] **Given** multiple backtest runs have been completed, **When** the Past Results tab loads, **Then** all runs are listed in reverse chronological order

## Out of Scope

- Past results pagination or filtering (future enhancement)
- Backtest result export
