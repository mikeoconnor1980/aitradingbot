# Consolidate Connection Indicator into Connection Pill

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **user**, I want **the connection status indicator consolidated into the existing connection pill (on the right side of the header)** so that **the header is cleaner and connection state is managed from a single control**.

## Problem Statement

The header currently has both a connection "bubble" and a separate connection pill, which is redundant. Removing the bubble and folding its functionality into the pill simplifies the UI and reduces clutter.

## Requirements

### Functional Requirements

- [ ] Remove the connection bubble/icon from the header
- [ ] The existing connection pill (right side of header) should display connection status (connected / disconnected / connecting)
- [ ] Clicking the pill should provide the same functionality the bubble currently offers (e.g. reconnect, view connection details)
- [ ] Connection status colour coding should transfer to the pill (green = connected, red = disconnected, amber = connecting)

### Non-Functional Requirements

- [ ] No change to underlying connection logic — this is purely a UI consolidation

## Acceptance Criteria

- [ ] **Given** the header is rendered, **When** the user looks at the header, **Then** there is no separate connection bubble — only the connection pill
- [ ] **Given** the user is connected, **When** they view the pill, **Then** it shows a connected state with green indicator
- [ ] **Given** the connection drops, **When** the pill updates, **Then** it shows a disconnected state and provides a way to reconnect

## Out of Scope

- Changing the connection logic or reconnection behaviour
- Moving the pill to a different position
