# Dashboard: Activity Date-Range Filter

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **trader**, I want **to filter the dashboard activity feed by a date range** so that **I can review trading activity for a specific period without scrolling through all history**.

## Problem Statement

The dashboard activity section currently shows all activity without any date filtering. As trading history grows, finding activity from a specific period becomes difficult. A date-range filter lets users focus on the window they care about.

## Requirements

### Functional Requirements

- [ ] Add a date-range picker to the dashboard activity section (start date + end date)
- [ ] Filtering should apply to the activity list/table and show only items within the selected range
- [ ] Provide sensible quick-select presets (e.g. Today, Last 7 days, Last 30 days, Custom)
- [ ] Default to a reasonable recent window (e.g. last 7 days) rather than all-time
- [ ] The selected date range should persist during the session (not reset on tab change)

### Non-Functional Requirements

- [ ] Filtering should feel instant for typical data volumes (client-side filtering if data is already loaded, or server-side with pagination for large datasets)

## Acceptance Criteria

- [ ] **Given** the dashboard activity section, **When** the user selects a date range, **Then** only activity within that range is displayed
- [ ] **Given** the user selects "Last 7 days", **When** the filter applies, **Then** only the last 7 days of activity are shown
- [ ] **Given** the user navigates away and returns to the dashboard, **When** the activity section loads, **Then** the previously selected date range is still applied

## Out of Scope

- Persisting the date range across browser sessions (localStorage)
- Exporting filtered activity to CSV
