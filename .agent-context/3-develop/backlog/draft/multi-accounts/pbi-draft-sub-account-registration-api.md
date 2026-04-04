# Sub-Account Registration & Management API

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **trader**, I want to register and manage multiple Hyperliquid sub-accounts so that I can allocate separate accounts for different trading strategies.

## Problem Statement

Currently the platform supports a single wallet credential per user. To enable multi-strategy execution on the same asset, users need the ability to register additional sub-accounts, each with their own Hyperliquid wallet key, and manage their lifecycle (activate, deactivate, label).

## Requirements

### Functional Requirements

1. API endpoint to create a new sub-account (provide wallet address, private key, label, purpose)
2. API endpoint to list all sub-accounts for the authenticated user
3. API endpoint to update a sub-account (label, purpose, active status)
4. API endpoint to deactivate a sub-account (soft delete — cannot deactivate if it has open positions)
5. API endpoint to query the balance/state of a specific sub-account from Hyperliquid
6. Validate that the provided wallet credentials are valid by performing a test read against Hyperliquid API

### Non-Functional Requirements

- Private keys are encrypted before storage using the existing encryption pattern
- All endpoints are tenant-scoped — users can only manage their own sub-accounts
- Rate-limited to prevent abuse of credential validation calls

## Acceptance Criteria

- [ ] **Given** an authenticated user, **When** they POST a new sub-account with valid credentials, **Then** the sub-account is created and the private key is encrypted at rest
- [ ] **Given** an authenticated user, **When** they GET their sub-accounts, **Then** all their sub-accounts are returned with balance snapshots (private keys never exposed)
- [ ] **Given** a sub-account with open positions, **When** the user tries to deactivate it, **Then** the request is rejected with a clear error message
- [ ] **Given** invalid Hyperliquid credentials, **When** creating a sub-account, **Then** validation fails with a descriptive error before persisting
- [ ] **Given** a sub-account, **When** queried for balance, **Then** the current balance is fetched from Hyperliquid `clearinghouseState` using that sub-account's wallet

### Release Notes Information

- **Heading**: Sub-Account Management
- **Release note type**: Feature
- **Release Note Summary**: Users can now register and manage multiple Hyperliquid sub-accounts, enabling strategy isolation across separate wallets.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/sub-accounts` | Create a new sub-account |
| GET | `/api/sub-accounts` | List all sub-accounts for user |
| GET | `/api/sub-accounts/{id}` | Get sub-account details + live balance |
| PUT | `/api/sub-accounts/{id}` | Update label/purpose/active |
| DELETE | `/api/sub-accounts/{id}` | Deactivate sub-account (soft) |

### Dependencies

- PBI: Sub-Account Domain Model (must be completed first)

## Out of Scope

- Fund transfers between sub-accounts (separate PBI)
- UI for sub-account management (part of Portfolio Dashboard PBI)
- Automatic sub-account creation during strategy deployment
