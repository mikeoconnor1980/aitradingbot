# Fund Transfer Engine (Treasury Service)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want to transfer funds between my sub-accounts so that I can allocate capital to the strategies that need it without withdrawing and re-depositing.

## Problem Statement

With multiple sub-accounts running different strategies, capital needs to flow between them. Manual transfers via the Hyperliquid UI are slow and error-prone. The platform needs an automated Treasury Service that can move funds between a user's sub-accounts, enforce minimum balance rules, and maintain an audit trail.

## Requirements

### Functional Requirements

1. API endpoint to initiate a fund transfer between two sub-accounts owned by the same user
2. Transfer validation: source account must have sufficient available balance (excluding margin in use)
3. Minimum balance enforcement: source account cannot go below a configurable minimum after transfer
4. Transfer status tracking: Pending → Confirmed → Failed
5. Idempotency: duplicate transfer requests (same idempotency key) are safely ignored
6. Audit log: every transfer attempt is recorded with timestamp, amounts, source, destination, status, and failure reason

### Non-Functional Requirements

- Transfers are executed via Hyperliquid's internal transfer API (USDC between sub-accounts)
- Retry logic with exponential backoff for transient API failures
- All transfer operations are tenant-scoped
- Transfer amounts are validated against exchange precision requirements

## Acceptance Criteria

- [ ] **Given** a user with two sub-accounts (A and B), **When** they request a transfer of 100 USDC from A to B, **Then** the balance of A decreases by 100 and B increases by 100
- [ ] **Given** a source account with 50 USDC available, **When** requesting a transfer of 100 USDC, **Then** the transfer is rejected with an insufficient balance error
- [ ] **Given** a transfer request with an idempotency key that was already processed, **When** submitted again, **Then** the original transfer result is returned without executing a duplicate
- [ ] **Given** a completed transfer, **When** querying the audit log, **Then** the transfer details (amount, source, destination, timestamp, status) are present
- [ ] **Given** a transient Hyperliquid API failure during transfer, **When** the retry succeeds, **Then** the transfer status transitions from Pending to Confirmed
- [ ] **Given** a source account that would go below minimum balance after transfer, **When** the transfer is requested, **Then** it is rejected

### Release Notes Information

- **Heading**: Fund Transfer Engine
- **Release note type**: Feature
- **Release Note Summary**: Automated fund transfers between sub-accounts with balance validation, retry logic, and full audit trail.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/transfers` | Initiate a fund transfer |
| GET | `/api/transfers` | List transfer history for user |
| GET | `/api/transfers/{id}` | Get transfer details |

### Entities

- `FundTransfer`: Id, UserId, SourceSubAccountId, DestinationSubAccountId, Amount, Currency, Status, IdempotencyKey, FailureReason, CreatedAtUtc, CompletedAtUtc

### Integration

- Hyperliquid internal transfer API (usdTransfer action type)

### Dependencies

- PBI: Sub-Account Domain Model
- PBI: Sub-Account Registration API

## Out of Scope

- Automatic rebalancing rules (separate PBI — Hedge Orchestration)
- External deposits/withdrawals from Hyperliquid
- Multi-currency transfers (USDC only for now)
