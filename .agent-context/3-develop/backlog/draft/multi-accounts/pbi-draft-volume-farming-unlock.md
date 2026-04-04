# Sub-Account Volume Farming Unlock

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T17:38:57Z

## User Story

As a **trader**, I want the platform to safely farm $100k cumulative trading volume on my master Hyperliquid account so that sub-accounts become unlocked and I can run isolated multi-strategy setups.

## Problem Statement

Hyperliquid requires $100k cumulative trading volume on the master account before sub-accounts can be created. Manually generating this volume is tedious and risky if done carelessly. The platform needs an automated volume farmer that places tight spread round-trip trades on BTC-PERP to accumulate volume safely, with minimal P&L impact and user oversight at regular intervals.

## Requirements

### Functional Requirements

1. A "Volume Farming" feature that places tight spread round-trip trades (limit buy near bid + limit sell near ask) on BTC-PERP to accumulate notional volume
2. Trade size is user-configurable within a safe range (minimum: Hyperliquid's minimum BTC-PERP size of 0.001 BTC)
3. Semi-automatic operation: the farmer pauses at every $10k volume milestone and requests user approval to continue
4. Per-trade loss limit: each round-trip has a maximum acceptable loss (configurable); if breached, the farmer cancels the open leg and pauses with a notification
5. Volume progress is tracked by querying the Hyperliquid API for the account's actual cumulative volume (not just local tracking)
6. When the $100k volume target is reached, the farmer auto-stops and sends a notification: "Sub-accounts are now unlocked!"
7. UI dashboard widget showing: current cumulative volume, percentage complete toward 100k, cumulative P&L cost of farming, estimated time remaining
8. User can start, pause, and stop the volume farmer from the UI
9. The farmer only runs when explicitly started — it is not tied to any strategy lifecycle

### Non-Functional Requirements

- Round-trip trades must use limit orders only (no market orders) to minimise slippage
- Orders are placed near the current mid-price with the tightest feasible spread to minimise P&L cost
- The farmer must cancel any unfilled or partially filled orders before pausing or stopping (clean shutdown)
- All trades placed by the farmer are tagged/labelled for auditability (distinguishable from strategy trades)
- The farmer uses the master account's existing credentials (not a sub-account)

## Acceptance Criteria

- [ ] **Given** a user with less than $100k cumulative volume, **When** they start the volume farmer, **Then** the farmer begins placing round-trip trades on BTC-PERP
- [ ] **Given** the farmer is running, **When** the volume crosses a $10k milestone (10k, 20k, 30k...), **Then** the farmer pauses and requests user approval to continue
- [ ] **Given** the user approves at a milestone, **When** approval is received, **Then** the farmer resumes trading
- [ ] **Given** a round-trip trade that exceeds the per-trade loss limit, **When** the loss is detected, **Then** the farmer cancels the open leg, pauses, and notifies the user
- [ ] **Given** the farmer is running, **When** cumulative volume reaches $100k, **Then** the farmer auto-stops and sends a "Sub-accounts unlocked!" notification
- [ ] **Given** the volume farming dashboard widget, **When** viewed, **Then** it displays current cumulative volume, % completion, P&L cost, and estimated time remaining
- [ ] **Given** the farmer is running, **When** the user clicks Stop, **Then** the farmer cancels all open orders and stops cleanly
- [ ] **Given** the farmer placed trades, **When** querying trade history, **Then** volume farming trades are distinguishable from strategy trades via a tag/label
- [ ] **Given** the user configures a trade size of 0.005 BTC, **When** the farmer places trades, **Then** each round-trip uses approximately 0.005 BTC notional

### Release Notes Information

- **Heading**: Sub-Account Volume Unlock Farming
- **Release note type**: Feature
- **Release Note Summary**: Automated volume farming feature that safely accumulates $100k trading volume on BTC-PERP to unlock Hyperliquid sub-accounts, with progress tracking and user oversight at $10k intervals.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/volume-farmer/start` | Start the volume farmer with configuration |
| POST | `/api/volume-farmer/stop` | Stop the volume farmer (clean shutdown) |
| POST | `/api/volume-farmer/approve` | Approve continuation at a milestone |
| GET | `/api/volume-farmer/status` | Get current farming status and progress |

### Volume Tracking

- Query Hyperliquid API for actual cumulative volume on the master account
- Local tracking as supplementary data (P&L cost, trade count, time elapsed)

### Trading Logic

- Place limit buy slightly below mid-price, limit sell slightly above mid-price
- Wait for both legs to fill (or timeout and cancel unfilled leg)
- Each completed round-trip contributes ~2x the notional size to cumulative volume
- Configurable trade interval to avoid rate limiting

### UI Components

- `VolumeFarmingWidgetComponent` — dashboard widget showing progress ring/bar, cumulative volume, P&L cost, ETA
- `VolumeFarmingControlsComponent` — start/stop/approve buttons
- Milestone approval modal or inline prompt

### Dependencies

- Existing `IHyperliquidSigner` and `IHyperliquidRestClient` for order placement
- Existing order placement infrastructure (used by strategies)

### Sequencing

- This PBI is standalone and can be developed independently of the sub-account PBIs
- It is a practical prerequisite for users who don't yet have 100k volume, but not a blocking dependency (sub-account PBIs can be developed in parallel using an account that already has the volume unlocked)

## Out of Scope

- Multi-asset volume farming (BTC-PERP only for v1)
- Automatic sub-account creation after volume is reached (user does this via PBI-02's registration API)
- Volume farming on sub-accounts (this feature targets the master account only)
