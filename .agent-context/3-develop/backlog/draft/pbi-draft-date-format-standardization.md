# Date Format Standardisation

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **user**, I want **all dates throughout the application to use a consistent `DD Month YYYY - HH:MM` format** so that **dates are easy to read and the experience feels polished and uniform**.

## Problem Statement

Dates are currently displayed in inconsistent formats across the frontend and backend (ISO strings, locale defaults, etc.). A single standardised format improves readability and gives a professional feel.

## Requirements

### Functional Requirements

- [ ] All user-facing dates across the Angular frontend must render as `DD Month YYYY - HH:MM` (e.g. `30 March 2026 - 16:22`)
- [ ] Create a shared Angular pipe or utility function that encapsulates the format
- [ ] Apply the pipe/utility to every component that displays a date (dashboard, positions, orders, backtesting, activity feed, etc.)
- [ ] API responses may remain ISO 8601; formatting is a frontend concern
- [ ] Dates in log files and developer tooling are not affected

### Non-Functional Requirements

- [ ] No user-facing date should use any other format after this change

## Acceptance Criteria

- [ ] **Given** any page in the application, **When** a date is displayed, **Then** it uses the `DD Month YYYY - HH:MM` format
- [ ] **Given** a date pipe/utility exists, **When** a new component needs to show a date, **Then** it can reuse the shared pipe

## Out of Scope

- Backend log or internal date formatting
- Timezone selection (all dates are UTC for now)
