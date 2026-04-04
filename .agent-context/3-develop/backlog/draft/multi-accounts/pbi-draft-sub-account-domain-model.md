# Sub-Account Domain Model & Entity Design

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T07:49:21Z

## User Story

As a **platform operator**, I want the domain model to support multiple Hyperliquid sub-accounts per user so that each strategy can run on an isolated account with its own positions.

## Problem Statement

Hyperliquid only supports a single net position per asset per account. To run multiple strategies on the same asset (e.g., grid + hedge), the platform needs a sub-account abstraction. The current domain model assumes a single exchange credential per user with no concept of sub-accounts.

## Requirements

### Functional Requirements

1. Introduce a `SubAccount` entity representing a Hyperliquid sub-account, scoped to a `User`
2. Each `SubAccount` has its own wallet address and encrypted private key (or derives from the master account)
3. Sub-accounts have a `Purpose` (e.g., `GridTrading`, `Hedging`, `Manual`) and a human-readable `Label`
4. Sub-accounts track their own `Balance` snapshot and `IsActive` status
5. The existing `UserExchangeCredential` entity is extended or related to support multiple credentials per user
6. All existing tenant-scoped entities (Order, Fill, Position, Signal) gain a `SubAccountId` foreign key

### Non-Functional Requirements

- Maintain backward compatibility — single-account users continue to work via a "default" sub-account
- Encrypted key storage pattern remains consistent with existing `UserExchangeCredential`
- Migration path from single-account to multi-account must be non-destructive

## Acceptance Criteria

- [ ] **Given** a registered user, **When** they have no explicit sub-accounts, **Then** a default sub-account is created automatically using their existing exchange credential
- [ ] **Given** a user with multiple sub-accounts, **When** querying orders/positions, **Then** results can be filtered by `SubAccountId`
- [ ] **Given** the `SubAccount` entity, **When** persisted, **Then** the encrypted private key follows the same encryption pattern as `UserExchangeCredential`
- [ ] **Given** the database migration, **When** applied to an existing database with data, **Then** existing records are associated with the default sub-account without data loss

### Release Notes Information

- **Heading**: Sub-Account Domain Model
- **Release note type**: Feature
- **Release Note Summary**: Introduces multi-sub-account support in the domain model, allowing each user to manage multiple isolated Hyperliquid accounts for strategy separation.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Entities

- `SubAccount`: Id, UserId, Label, Purpose, WalletAddress, EncryptedPrivateKey, Balance, IsActive, CreatedAtUtc
- Extend Order, Fill, Position, Signal with `SubAccountId`

### Database Migration

- New `SubAccounts` table
- Add `SubAccountId` column to existing trading tables
- Data migration: create default sub-account per existing user, backfill FK

## Out of Scope

- Fund transfer logic (separate PBI)
- Portfolio aggregation logic (separate PBI)
- UI for managing sub-accounts (separate PBI)
- Hyperliquid sub-account API integration (separate PBI)
