# Fund Transfer Engine (Treasury Service)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z
**Updated:** 2026-04-04T00:00:00Z

## User Story

As a **trader**, I want to transfer funds between my sub-accounts so that I can allocate capital to the strategies that need it without withdrawing and re-depositing.

## Problem Statement

With multiple sub-accounts running different strategies, capital needs to flow between them. Manual transfers via the Hyperliquid UI are slow and error-prone. The platform needs an automated Treasury Service that can move funds between a user's sub-accounts, enforce per-sub-account minimum balance rules, and maintain an audit trail — with a supporting UI in the dashboard for initiating and monitoring transfers.

## Requirements

### Functional Requirements

1. API endpoint to initiate a fund transfer between two sub-accounts owned by the same user
2. Transfer validation: source account must have sufficient available balance (excluding margin in use)
3. Per-sub-account minimum balance enforcement: each sub-account has a configurable minimum balance floor; source account cannot go below its configured minimum after the transfer
4. Transfer amount is constrained only by available balance and the minimum balance rule (no separate maximum cap)
5. Transfer status tracking: Pending → Confirmed → Failed
6. Idempotency: duplicate transfer requests (same idempotency key) are safely ignored and return the original result
7. Automatic retry with exponential backoff for transient Hyperliquid API failures
8. Manual retry: user can retry a failed transfer from the UI after automatic retries are exhausted
9. Audit log: every transfer attempt is recorded with timestamp, amount, source, destination, status, and failure reason
10. Transfer UI: Angular dashboard page with a transfer form showing source/destination sub-account selector and amount input
11. Transfer history UI: paginated list of past transfers with status indicators
12. Real-time status updates: transfer status is reflected in the UI via polling after submission
13. In-app notification when a transfer completes or fails

### Non-Functional Requirements

- Transfers are executed via Hyperliquid's internal transfer API (usdTransfer action type)
- All transfer operations are tenant-scoped by UserId
- Transfer amounts are validated against exchange precision requirements (USDC)
- Minimum balance configuration is stored per sub-account and editable by the user

## Acceptance Criteria

- [ ] **Given** a user with two sub-accounts (A and B), **When** they submit a transfer of 100 USDC from A to B via the UI, **Then** the transfer is submitted immediately and status updates are shown in real time
- [ ] **Given** a transfer is submitted, **When** Hyperliquid confirms it, **Then** the status transitions from Pending to Confirmed and the user receives an in-app notification
- [ ] **Given** a source account with 50 USDC available, **When** requesting a transfer of 100 USDC, **Then** the transfer is rejected with an insufficient balance error
- [ ] **Given** a source account with a minimum balance of 20 USDC and 80 USDC available, **When** requesting a transfer of 70 USDC, **Then** the transfer is rejected because it would violate the minimum balance floor
- [ ] **Given** a source account with a minimum balance of 20 USDC and 80 USDC available, **When** requesting a transfer of 60 USDC, **Then** the transfer is accepted (leaves exactly the minimum)
- [ ] **Given** a transfer request with an idempotency key that was already processed, **When** submitted again, **Then** the original transfer result is returned without executing a duplicate
- [ ] **Given** a completed transfer, **When** querying the transfer history, **Then** the transfer details (amount, source, destination, timestamp, status) are present in the audit log
- [ ] **Given** a transient Hyperliquid API failure during transfer, **When** the automatic retry succeeds, **Then** the transfer status transitions from Pending to Confirmed
- [ ] **Given** a transfer that has exhausted all automatic retries, **When** the user clicks "Retry" in the UI, **Then** a new transfer attempt is initiated
- [ ] **Given** a transfer failure after all retries, **When** the failure occurs, **Then** the user receives an in-app notification with the failure reason
- [ ] **Given** a user navigates to the transfer history page, **When** the page loads, **Then** transfers are displayed in descending order by date with status badges

### Release Notes Information

- **Heading**: Fund Transfer Engine
- **Release note type**: Feature
- **Release Note Summary**: Traders can now transfer USDC between sub-accounts directly from the dashboard, with per-sub-account minimum balance protection, automatic retry on failure, and a full audit trail.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/transfers` | Initiate a fund transfer |
| GET | `/api/transfers` | List transfer history for user (paginated) |
| GET | `/api/transfers/{id}` | Get transfer details |
| POST | `/api/transfers/{id}/retry` | Manually retry a failed transfer |
| PUT | `/api/sub-accounts/{id}/minimum-balance` | Set the minimum balance for a sub-account |

### Entities

- `FundTransfer`: Id, UserId, SourceSubAccountId, DestinationSubAccountId, Amount, Currency, Status, IdempotencyKey, FailureReason, RetryCount, CreatedAtUtc, CompletedAtUtc
- `SubAccountSettings` (or update existing sub-account entity): MinimumBalance per sub-account

### UI Components

- Transfer form: sub-account dropdowns (source/destination), amount input, submit button
- Transfer status indicator: real-time status display (polling after submit)
- Transfer history list: paginated, sortable by date, with status badges and retry button for failed transfers
- In-app notification: toast or notification panel entry on Confirmed/Failed

### Integration

- Hyperliquid internal transfer API (`usdTransfer` action type)

### Dependencies

- PBI: Sub-Account Domain Model
- PBI: Sub-Account Registration API

## Out of Scope

- Automatic rebalancing rules (separate PBI — Hedge Orchestration)
- External deposits/withdrawals from Hyperliquid
- Multi-currency transfers (USDC only for now)
- Email notifications (in-app only for this PBI)
- WebSocket/SignalR push for status updates (polling is sufficient for MVP)
