# Bug: Selecting More Than 2 Past Results Flicks to Different Tab

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## Bug Summary

When selecting past backtest results for comparison, clicking a third result causes the UI to unexpectedly switch to a different tab. If two is the maximum number of results that can be compared, the UI should communicate this limit clearly rather than exhibiting broken behaviour.

## Steps to Reproduce

1. Navigate to the Backtesting page → Past Results tab
2. Select one past result (checkbox or click)
3. Select a second past result
4. Attempt to select a third past result
5. **Expected:** Either the third selection is allowed, or a message informs the user that the maximum is 2
6. **Actual:** The UI flicks/switches to a different tab unexpectedly

## Requirements

### Functional Requirements

- [ ] Determine the intended maximum number of selectable past results for comparison
- [ ] If the max is 2: disable further selection after 2 are selected, and show an inline message (e.g. "Maximum of 2 results can be compared")
- [ ] If the max should be higher: fix the bug that causes the tab switch when selecting a 3rd result
- [ ] Selecting/deselecting should never cause an unintended tab switch

### Investigation Notes

- Likely a click event propagation issue or a tab index collision with the selection handler
- Check the comparison component's selection logic

## Acceptance Criteria

- [ ] **Given** 2 past results are already selected, **When** the user tries to select a 3rd, **Then** they see a clear message about the limit (or the selection succeeds if the limit is raised)
- [ ] **Given** the user is on the Past Results tab, **When** they interact with result selection, **Then** the tab does not change unexpectedly

## Out of Scope

- Increasing the comparison limit beyond current design intent
- Redesigning the comparison UI
