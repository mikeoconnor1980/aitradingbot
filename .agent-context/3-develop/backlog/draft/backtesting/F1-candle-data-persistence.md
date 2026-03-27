# PBI Specification: F1 — Candle Data Persistence

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-27
**PRD:** [candle-persistence-backtesting-prd.md](../../prd/candle-persistence-backtesting-prd.md)
**Implementation Phase:** 1 (Foundation)
**Risk Level:** Low
**Depends On:** None

---

## Summary

Introduce the `Candle` domain entity, set up EF Core with SQLite in the Persistence layer, and expose a repository interface for querying and bulk-inserting candle data. This is the data foundation for candle ingestion and backtesting.

### User Story

> As the **backtest engine** (internal consumer), I want to **store and query OHLCV candle data locally in a SQLite database** so that **candle data is available for replay without hitting the Hyperliquid API every time**.

### Business Value

Eliminates redundant API calls to Hyperliquid for candle data, enables offline access to historical market data, and provides the storage foundation required by the candle ingestion service (F2) and the backtest replay engine (F3). Without this, no backtesting or local data analysis is possible.

---

## Problem Statement

The platform currently fetches candle data from Hyperliquid's REST API on demand but does not persist it. Every candle request hits the exchange. There is no database context, no domain entity for candles, and no repository to query historical data. This blocks both local data caching and backtesting.

---

## Requirements

### Functional Requirements

- [ ] A `Candle` domain entity exists in `TradingApp.Domain` with properties: `Id` (long, auto-increment), `Symbol` (string, max 20), `Interval` (string, max 10), `Timestamp` (long, unix ms — candle open time), `Open` (decimal), `High` (decimal), `Low` (decimal), `Close` (decimal), `Volume` (decimal), `NumTrades` (int)
- [ ] A `TradingAppDbContext` exists in `TradingApp.Persistence` using the `Microsoft.EntityFrameworkCore.Sqlite` provider
- [ ] The `Candle` entity is configured in the DbContext with a composite unique index on (`Symbol`, `Interval`, `Timestamp`) to prevent duplicate candle entries
- [ ] EF Core migrations are applied automatically on startup via `context.Database.MigrateAsync()`
- [ ] The SQLite database file path is configured via `appsettings.json` under `ConnectionStrings:DefaultConnection` (default: `Data Source=Data/tradingapp.db`, relative to working directory)
- [ ] An `ICandleRepository` interface is defined in `TradingApp.Application` with methods: `GetCandlesAsync(symbol, interval, startTime, endTime)` returning candles ordered by Timestamp ascending, `BulkInsertAsync(candles)` for batch insertion, and `GetLatestTimestampAsync(symbol, interval)` returning the most recent candle timestamp for a given symbol/interval
- [ ] A `CandleRepository` implementation exists in `TradingApp.Persistence` implementing `ICandleRepository`
- [ ] `BulkInsertAsync` uses `INSERT OR IGNORE` semantics — duplicates are skipped based on the composite unique index (no errors on re-insert)
- [ ] `BulkInsertAsync` processes large candle sets in batches of 500 rows per transaction to avoid SQLite parameter limits
- [ ] `GetCandlesAsync` returns an empty collection when no candles exist for the requested range
- [ ] `GetLatestTimestampAsync` returns `null` when no candles exist for the given symbol/interval
- [ ] The `TradingAppDbContext` and `ICandleRepository` are registered in the DI container via a Persistence layer extension method
- [ ] The `Data/` directory for the SQLite database file is created automatically if it does not exist

### Non-Functional Requirements

- [ ] All database operations use async EF Core APIs
- [ ] Candle prices use `decimal` (not `double`) to avoid floating-point precision issues in PnL calculations
- [ ] The SQLite database file remains under 50 MB for the full expected dataset (~138K candles)
- [ ] The composite unique index ensures duplicate prevention without application-level locking

### Design Decisions

- **Multi-tenancy exemption**: The `Candle` entity does NOT have a `UserId` column. Candle data is shared public market data, identical for all subscribers. ADR 6 tenant scoping applies to user-specific data (strategies, orders, positions), not reference market data.
- **Single-writer model**: Only the Worker process writes candle data (via the ingestion service). The API process is a read-only consumer. No write contention on the SQLite file.
- **Interval values**: The `Interval` column accepts any string value (e.g., `15m`, `1h`, `4h`, `1d`). No domain-level restriction — the ingestion service (F2) controls which intervals are fetched.
- **No retention policy**: All candles are kept indefinitely. The expected dataset (~138K candles) fits well within the 50 MB SQLite budget.
- **Hosting registration**: Both `TradingApp.Api` and `TradingApp.Worker` register the `TradingAppDbContext` via the shared `AddPersistence(configuration)` extension method. The API needs read access for F4 (Backtest API).
- **Bulk insert batching**: `BulkInsertAsync` processes inserts in batches of 500 rows per transaction using `INSERT OR IGNORE` to skip duplicates at the SQLite level.

---

## User Flow

### Happy Path

1. Application starts and the `TradingAppDbContext` is initialized
2. EF Core migrations run (or have been applied) and the `Candles` table exists in `Data/tradingapp.db`
3. The candle ingestion service (F2) calls `BulkInsertAsync` to insert candle batches — duplicates are silently skipped
4. The backtest replay engine (F3) calls `GetCandlesAsync` with a symbol, interval, and date range — candles are returned ordered by timestamp ascending
5. The ingestion service calls `GetLatestTimestampAsync` to determine where to resume fetching

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| SQLite database file path directory does not exist | Directory is created automatically on first access |
| Duplicate candle insert attempted | Duplicate is silently skipped (no exception), insertion continues |
| Query for non-existent date range | Empty collection returned |
| Connection string missing from config | Application fails to start with a clear configuration error |
| Database file locked by another process | EF Core throws `SqliteException`; caller handles retry or propagates error |

---

## Technical Considerations

### Entity Design

```
Candle
├── Id (long, PK, auto-increment)
├── Symbol (string, max 20)
├── Interval (string, max 10)
├── Timestamp (long, unix ms — candle open time)
├── Open (decimal)
├── High (decimal)
├── Low (decimal)
├── Close (decimal)
├── Volume (decimal)
└── NumTrades (int)

Index: IX_Candle_Symbol_Interval_Timestamp (unique)
```

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `Candle` | `TradingApp.Domain` | Domain entity representing a single OHLCV candle |
| `TradingAppDbContext` | `TradingApp.Persistence` | EF Core DbContext with SQLite provider; configures `Candle` entity and composite index |
| `ICandleRepository` | `TradingApp.Application` | Repository interface for candle data access |
| `CandleRepository` | `TradingApp.Persistence` | EF Core implementation of `ICandleRepository` with upsert semantics |
| `PersistenceServiceExtensions` | `TradingApp.Persistence` | DI registration extension method for DbContext and repository |

### Configuration Shape

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Data/tradingapp.db"
  }
}
```

### NuGet Packages

| Package | Project | Purpose |
|---------|---------|---------|
| `Microsoft.EntityFrameworkCore.Sqlite` | `TradingApp.Persistence` | SQLite EF Core provider |
| `Microsoft.EntityFrameworkCore.Design` | `TradingApp.Persistence` | Migration tooling |
| `Microsoft.EntityFrameworkCore` | `TradingApp.Persistence` | Core EF framework |

### Migration Path

SQLite is used for POC/development. Migration to Azure SQL requires only a provider swap (`Microsoft.EntityFrameworkCore.SqlServer`) and connection string change, per ADR 3.

### Testing Approach

Integration tests for `CandleRepository` use an in-memory SQLite database (`DataSource=:memory:`) with the real `TradingAppDbContext`. This validates EF Core mappings, index behavior, and upsert semantics without file I/O.

---

## Out of Scope

- Candle ingestion logic (covered by F2)
- Backtest engine or replay logic (covered by F3)
- API endpoints for candle data (covered by F2 and F4)
- Multi-asset support beyond BTC
- Real-time candle persistence (auto-sync on each candle close)
- Funding rate, order book, or sentiment data entities
- Database seeding or sample data

---

## Open Questions

- [x] ~~Should the `Data/` directory be relative to the working directory or configurable as an absolute path?~~ **Resolved:** Relative to the working directory. The `Data Source=Data/tradingapp.db` connection string uses a path relative to the app's working directory. Simple and portable for POC/dev.
- [x] ~~Should the initial migration be applied automatically on startup via `context.Database.MigrateAsync()` or require manual `dotnet ef database update`?~~ **Resolved:** Auto-migrate on startup. Call `context.Database.MigrateAsync()` during application startup. Zero friction for dev/POC and works well in Docker/CI scenarios.

---

## Acceptance Criteria

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

### Release Notes Information

- **Heading**: Candle Data Persistence (SQLite)
- **Release note type**: Feature
- **Release Note Summary**: Introduced local SQLite storage for OHLCV candle data with EF Core, enabling persistent candle history for backtesting and reducing exchange API dependency.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F2** — Candle Ingestion Service depends on the repository and DbContext created here
- **F3** — Backtest Replay Engine reads candle data from the repository created here
- **F4** — Backtest API depends indirectly on candle data being available in the database
