---
applyTo: ".agent-context/3-develop/build/changes/20260327-candle-data-persistence-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-03-27T22:30:00Z"
status: "complete"
lastUpdated: "2026-03-27T22:27:14Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Candle Data Persistence

## Overview

Introduce the `Candle` domain entity, set up EF Core with SQLite in the Persistence layer, expose a repository interface for querying and bulk-inserting candle data, and wire persistence into both API and Worker hosts with auto-migration on startup.

## PBI Details

**PBI ID:** Draft
**PBI File:** `.agent-context/3-develop/backlog/draft/backtesting/F1-candle-data-persistence.md`
**Implementation Phase:** 1 (Foundation)
**Risk Level:** Low
**Depends On:** None

### Summary

Introduce the `Candle` domain entity, set up EF Core with SQLite in the Persistence layer, and expose a repository interface for querying and bulk-inserting candle data. This is the data foundation for candle ingestion and backtesting.

### Acceptance Criteria

- [ ] **Given** the application starts, **When** the persistence layer initializes, **Then** the `TradingAppDbContext` connects to a SQLite database at the configured path
- [ ] **Given** no database file exists, **When** migrations are applied, **Then** the `Candles` table is created with the correct schema including the composite unique index
- [ ] **Given** a batch of candle data, **When** `BulkInsertAsync` is called, **Then** all candles are persisted to the database
- [ ] **Given** a batch containing duplicate candles, **When** `BulkInsertAsync` is called, **Then** duplicates are silently skipped and new candles are inserted without error
- [ ] **Given** candles exist in the database, **When** `GetCandlesAsync` is called with a valid symbol, interval, and date range, **Then** matching candles are returned ordered by Timestamp ascending
- [ ] **Given** no candles exist for the requested range, **When** `GetCandlesAsync` is called, **Then** an empty collection is returned
- [ ] **Given** candles exist in the database, **When** `GetLatestTimestampAsync` is called, **Then** the most recent candle timestamp for the given symbol/interval is returned
- [ ] **Given** no candles exist for a symbol/interval, **When** `GetLatestTimestampAsync` is called, **Then** `null` is returned
- [ ] **Given** the `Candle` entity, **When** inspecting its properties, **Then** all price and volume fields use `decimal` type
- [ ] **Given** the unique index on (`Symbol`, `Interval`, `Timestamp`), **When** two candles with the same key are inserted, **Then** the second insert is rejected at the database level
- [ ] **Given** a large batch of 1000+ candles, **When** `BulkInsertAsync` is called, **Then** candles are inserted in batches of 500 per transaction
- [ ] **Given** both `TradingApp.Api` and `TradingApp.Worker` hosts, **When** the persistence layer is registered, **Then** both hosts can resolve `TradingAppDbContext` and `ICandleRepository`

## Objectives

- Create the first domain entity (`Candle`) following existing codebase conventions
- Establish the EF Core SQLite persistence infrastructure from scratch
- Implement `ICandleRepository` with `INSERT OR IGNORE` bulk insert semantics
- Wire persistence into both API and Worker hosts with auto-migration on startup
- Establish the persistence integration test pattern for future features

### Discovery References

- **Domain Model ERD**: `.agent-context/0-knowledge/data-model/data-model-erd.md` — `Candle` entity defined with slight field-name differences from F1 spec (F1 spec is authoritative)
- **ADR 3**: SQLite for POC → Azure SQL for production; EF Core abstracts both providers
- **ADR 6**: Multi-tenancy by UserId — `Candle` entity is explicitly exempt (shared public market data)
- **Backtesting Architecture**: `.agent-context/0-knowledge/18-backtesting-architecture.md` — `HistoricalDataProvider` consumes candle data from repository
- **Scheduling Architecture**: `.agent-context/0-knowledge/19-scheduling-architecture.md` — `CandleClosedEvent` carries a `Candle` domain object
- **INSERT OR IGNORE**: EF Core has no high-level abstraction; raw parameterized SQL with `ExecuteSqlRawAsync` is required for `INSERT OR IGNORE` semantics
- **SQLite Decimal Storage**: SQLite has no native `DECIMAL` type; use `HasConversion<double>()` for OHLCV prices to enable server-side range queries while keeping `decimal` in the C# entity model
- **Validation**: Codebase uses `ArgumentException.ThrowIfNullOrWhiteSpace` (not Ardalis.GuardClauses)

### Project Patterns

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface location pattern for dependency-inverted services
- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — Existing DTO pattern (sealed class, init-only props)
- `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` — CQRS query/handler co-location pattern
- `src/TradingApp.Api/Program.cs` — DI registration pattern (inline, first extension method will be `AddPersistence`)
- `src/TradingApp.Worker/Program.cs` — Bare host (3 lines, needs persistence wiring)
- `tests/TradingApp.Infrastructure.Tests/Services/NonceProviderTests.cs` — Unit test pattern (MSTest sealed class, Given_When_Then)
- `tests/TradingApp.Api.Tests/Services/HyperliquidAccountServiceTests.cs` — Unit test with mocks pattern ([TestInitialize] setup)
- `src/TradingApp.Api/Services/MarketDataStreamService.cs` — `IServiceScopeFactory.CreateScope()` pattern for scoped services

### [x] Phase 1: Domain Entity, Persistence Layer & Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create the `Candle` domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-11-create-the-candle-domain-entity

- [x] Task 1.2: Add EF Core NuGet packages to Persistence project
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-12-add-ef-core-nuget-packages-to-persistence-project

- [x] Task 1.3: Create `TradingAppDbContext` with Candle entity configuration
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-13-create-tradingappdbcontext-with-candle-entity-configuration

- [x] Task 1.4: Create `ICandleRepository` interface
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-14-create-icandlerepository-interface

- [x] Task 1.5: Create `CandleRepository` implementation with INSERT OR IGNORE bulk insert
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-15-create-candlerepository-implementation-with-insert-or-ignore-bulk-insert

- [x] Task 1.6: Create `PersistenceServiceExtensions` for DI registration
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-16-create-persistenceserviceextensions-for-di-registration

- [x] Task 1.7: Generate the initial EF Core migration
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-17-generate-the-initial-ef-core-migration

- [x] Task 1.8: Create `TradingApp.Persistence.Tests` project and `CandleRepository` integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-18-create-persistence-tests-project-and-candlerepository-integration-tests

- [x] Task 1.9: Create `Candle` domain entity tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-19-create-candle-domain-entity-tests

- [x] Task 1.10: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-01-details.md#task-110-build-solution-and-run-all-tests

### [x] Phase 2: Host Registration, Startup Migration & Configuration

**Complexity**: Low | **Risk**: Low

- [x] Task 2.1: Add Persistence project references to Api and Worker
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-21-add-persistence-project-references-to-api-and-worker

- [x] Task 2.2: Add connection string configuration to appsettings files
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-22-add-connection-string-configuration-to-appsettings-files

- [x] Task 2.3: Register persistence services and add startup migration to Api
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-23-register-persistence-services-and-add-startup-migration-to-api

- [x] Task 2.4: Register persistence services and add startup migration to Worker
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-24-register-persistence-services-and-add-startup-migration-to-worker

- [x] Task 2.5: Add `Data/*.db*` to `.gitignore`
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-25-add-database-files-to-gitignore

- [x] Task 2.6: Build solution, run all tests, and verify API startup
  - Details: .agent-context/3-develop/build/plans/details/20260327-candle-data-persistence-phase-02-details.md#task-26-build-solution-run-all-tests-and-verify-api-startup

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Domain Entity, Persistence Layer & Tests | Medium | Low |
| Phase 2: Host Registration, Startup Migration & Configuration | Low | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- Persistence layer is a clean slate — no existing DbContext, migrations, or EF Core packages to conflict with
- INSERT OR IGNORE requires raw SQL via `ExecuteSqlRawAsync` — no EF Core high-level abstraction exists
- SQLite decimal columns need `HasConversion<double>()` for server-side range query support; C# entity model keeps `decimal`
- SQLite parameter limit is 32,766 on modern versions (bundled via `Microsoft.Data.Sqlite`); 500 rows × 9 columns = 4,500 params is safe
- In-memory SQLite testing requires open connection + `EnsureCreated()` (not `MigrateAsync()`)
- New test project `TradingApp.Persistence.Tests` establishes the persistence integration test pattern

## Dependencies

- `Microsoft.EntityFrameworkCore.Sqlite` (v8.x) — SQLite EF Core provider
- `Microsoft.EntityFrameworkCore.Design` (v8.x) — Migration tooling (PrivateAssets=all)
- `Microsoft.EntityFrameworkCore` (v8.x) — Core EF framework (transitive via Sqlite)
- `Microsoft.EntityFrameworkCore.Sqlite` (v8.x) in test project — In-memory SQLite testing

## Success Criteria

- `dotnet build TradingApp.sln` succeeds with no errors
- All existing tests continue to pass
- All new persistence integration tests pass (bulk insert, duplicate handling, range queries, latest timestamp)
- All new domain entity tests pass (factory method validation, property types)
- API starts successfully and creates `Data/tradingapp.db` with `Candles` table
- Worker starts successfully and can resolve `TradingAppDbContext` and `ICandleRepository`

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-03-27T20:28:18Z | 2026-03-27T20:35:00Z |
| Plan Reviewer | plan-reviewed | 2026-03-27T21:10:00Z | 2026-03-27T21:11:00Z |
| 3-Develop: 2 Implementer | completed | 2026-03-27T21:15:00Z | 2026-03-27T22:00:00Z |
