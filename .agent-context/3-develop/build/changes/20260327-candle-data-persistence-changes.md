<!-- markdownlint-disable-file -->
# Release Changes: Candle Data Persistence

**Related Plan**: 20260327-candle-data-persistence-plan.instructions.md
**Implementation Date**: 2026-03-27

## Summary

Introduces the `Candle` domain entity, EF Core SQLite persistence infrastructure, `ICandleRepository` with INSERT OR IGNORE bulk insert semantics, and wires persistence into both API and Worker hosts with auto-migration on startup.

## Changes

### Added

<!-- Phase 1: Domain Entity, Persistence Layer & Tests -->
- src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs: Added repository abstraction for candle query, bulk insert, and latest timestamp retrieval.
- src/TradingApp.Persistence/TradingAppDbContext.cs: Added EF Core DbContext with Candle mapping, unique composite index, and decimal-to-double conversions for SQLite.
- src/TradingApp.Persistence/Repositories/CandleRepository.cs: Added repository implementation with batch INSERT OR IGNORE and query methods.
- src/TradingApp.Persistence/PersistenceServiceExtensions.cs: Added DI registration extension for DbContext and ICandleRepository.
- src/TradingApp.Persistence/DesignTimeDbContextFactory.cs: Added design-time factory for EF migration tooling.
- src/TradingApp.Persistence/Migrations/20260327214340_InitialCreate.cs: Added initial migration creating Candles table and unique composite index.
- src/TradingApp.Persistence/Migrations/20260327214340_InitialCreate.Designer.cs: Added migration designer metadata.
- src/TradingApp.Persistence/Migrations/TradingAppDbContextModelSnapshot.cs: Added EF model snapshot.
- tests/TradingApp.Persistence.Tests/TradingApp.Persistence.Tests.csproj: Added new persistence integration test project.
- tests/TradingApp.Persistence.Tests/Usings.cs: Added global test usings.
- tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs: Added integration tests for bulk insert, duplicate skip, range query, latest timestamp, batch behavior, and decimal precision.
- tests/TradingApp.Domain.Tests/Entities/CandleTests.cs: Added domain unit tests for Candle.Create factory behavior and guard clauses.

### Modified

<!-- Phase 1: Domain Entity, Persistence Layer & Tests -->
- src/TradingApp.Domain/Entities/Candle.cs: Completed entity with Id, NumTrades, private parameterless constructor, private setters, and static Create factory with validation.
- src/TradingApp.Persistence/TradingApp.Persistence.csproj: Added Microsoft.EntityFrameworkCore.Sqlite and Microsoft.EntityFrameworkCore.Design package references.
- tests/TradingApp.Application.Tests/Scheduling/CandleClockTests.cs: Updated test candle creation to use Candle.Create factory.
- src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs: Resolved Domain type ambiguity via explicit alias for Nethereum EIP-712 Domain type.
- TradingApp.sln: Added TradingApp.Persistence.Tests project.
- Directory.Build.props: Added artifacts exclusion support during cleanup workflow.
- Directory.Build.targets: Added compile-time exclusion of temporary artifacts-generated files.

<!-- Phase 2: Host Registration, Startup Migration & Configuration -->
- .gitignore: Added SQLite ignore patterns for Data folder database artifacts.
- src/TradingApp.Api/TradingApp.Api.csproj: Added project reference to TradingApp.Persistence.
- src/TradingApp.Worker/TradingApp.Worker.csproj: Added project reference to TradingApp.Persistence.
- src/TradingApp.Api/appsettings.json: Added ConnectionStrings.DefaultConnection for SQLite path Data/tradingapp.db.
- src/TradingApp.Api/appsettings.Development.json: Added ConnectionStrings.DefaultConnection for SQLite path Data/tradingapp.db.
- src/TradingApp.Worker/appsettings.json: Added ConnectionStrings.DefaultConnection for SQLite path Data/tradingapp.db.
- src/TradingApp.Worker/appsettings.Development.json: Added ConnectionStrings.DefaultConnection for SQLite path Data/tradingapp.db.
- src/TradingApp.Api/Program.cs: Registered AddPersistence and added startup scope to create Data directory and run MigrateAsync before pipeline start.
- src/TradingApp.Worker/Program.cs: Registered AddPersistence and added startup scope to create Data directory and run MigrateAsync before host run.

### Removed

## Test Results

<!-- Phase 1: Domain Entity, Persistence Layer & Tests -->
- TradingApp.Domain.Tests: 7/7 passed
- TradingApp.Application.Tests: 5/5 passed
- TradingApp.Persistence.Tests: 8/8 passed
- TradingApp.Infrastructure.Tests: 30/30 passed
- TradingApp.Api.Tests: 49/49 passed

<!-- Phase 2: Host Registration, Startup Migration & Configuration -->
- TradingApp.Domain.Tests: 7/7 passed
- TradingApp.Application.Tests: 5/5 passed
- TradingApp.Persistence.Tests: 8/8 passed
- TradingApp.Infrastructure.Tests: 30/30 passed
- TradingApp.Api.Tests: 49/49 passed
- API startup: Candles table created and migrations applied successfully

## Issues

<!-- Phase 1: Domain Entity, Persistence Layer & Tests -->
- Build initially failed because existing test code instantiated Candle via object initializer; resolved by updating CandleClockTests to use Candle.Create.
- Build initially failed in HyperliquidEip712 due Domain namespace/type ambiguity; resolved with explicit alias to Nethereum.ABI.EIP712.Domain.
- Running API process locked Debug binaries during solution-wide validation; resolved by running build/test in Release configuration.
- Temporary artifacts output redirection created files picked up by wildcard compile includes; resolved by excluding artifacts content via Directory.Build.targets.

<!-- Phase 2: Host Registration, Startup Migration & Configuration -->
- dotnet build initially failed in Debug because running API process locked Debug output assemblies. Resolved by using Release configuration.
- sqlite3 CLI unavailable for direct schema verification; confirmed via EF migration startup logs showing CREATE TABLE Candles and CREATE UNIQUE INDEX IX_Candles_Symbol_Interval_Timestamp.

## Design Decisions

<!-- Phase 1: Domain Entity, Persistence Layer & Tests -->
- Added explicit EIP712 domain type alias in HyperliquidEip712 as a minimal/safe compile fix to keep behavior unchanged.
- Added repository integration tests with in-memory SQLite and EnsureCreated to validate real EF mappings and INSERT OR IGNORE behavior without requiring host startup.
- Added global artifacts exclusion in Directory.Build.targets to prevent accidental compilation of temporary generated files.

<!-- Phase 2: Host Registration, Startup Migration & Configuration -->
- Startup migration implemented by resolving TradingAppDbContext from a scoped service provider, deriving DB directory from connection string, ensuring directory exists, then running MigrateAsync before app pipeline start.
- Used relative path Data/tradingapp.db in all appsettings files to match infrastructure guidance.

## Review Hints

- Review src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs for the Domain alias change to confirm it aligns with existing EIP-712 conventions.
- Review Directory.Build.targets to confirm repository-level acceptance of excluding artifacts directories from compile/content items.
- Review src/TradingApp.Api/Program.cs startup migration block placement to confirm it runs before middleware and app run.
- Review src/TradingApp.Worker/Program.cs startup migration block and host boot sequence.
- src/TradingApp.Api/appsettings.Development.json contains a development private key already in repo state; confirm handling aligns with team security policy.

## Release Summary

Candle Data Persistence foundation is fully implemented across 2 phases, 16 tasks. The `Candle` domain entity is created with a static `Create` factory and full validation. EF Core SQLite persistence infrastructure is established in `TradingApp.Persistence` with `TradingAppDbContext`, composite unique index, decimal-to-double conversions, and `CandleRepository` implementing batch INSERT OR IGNORE semantics. Both API and Worker hosts now auto-migrate on startup and create `Data/tradingapp.db`. A new `TradingApp.Persistence.Tests` project establishes the integration test pattern with 8 passing tests. All 99 existing tests continue to pass.
