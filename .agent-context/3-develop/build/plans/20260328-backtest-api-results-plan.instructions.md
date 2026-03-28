applyTo: ".agent-context/3-develop/build/changes/20260328-backtest-api-results-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-28T15:33:24Z"
status: "complete"
lastUpdated: "2026-03-28T16:07:40Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F4 — Backtest API & Results

## Overview

Expose the backtest replay engine via HTTP API endpoints with result persistence, data coverage validation, and result retrieval — enabling programmatic strategy backtesting against persisted Binance candle data.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Feature:** F4 — Backtest API & Results
**Depends On:** F1 (Candle Data Persistence — complete), F3 (Backtest Replay Engine — interface only, no implementation)

Expose the backtest replay engine (F3) via HTTP API endpoints that accept strategy configuration and date range parameters, run the backtest synchronously, persist the result to SQLite, and return a structured response with summary metrics and a full trade log. Additionally, provide a retrieval endpoint for previously saved results and a validation endpoint to check candle data coverage before running.

### Acceptance Criteria

- [ ] **Given** valid parameters, **When** `POST /api/backtests` is called, **Then** a backtest runs, the result is persisted to SQLite, and the full result (including `id`) is returned with 200 status
- [ ] **Given** an invalid date range (end before start), **When** the endpoint is called, **Then** a 400 error is returned with a descriptive message
- [ ] **Given** no candle data exists for the requested range, **When** the endpoint is called, **Then** a 404 error is returned with a message indicating missing data
- [ ] **Given** an invalid strategy config (e.g., gridLevels = 0), **When** the endpoint is called, **Then** a 400 error is returned with field-level config validation details
- [ ] **Given** a missing required field, **When** the endpoint is called, **Then** a 400 error is returned with field-level validation errors
- [ ] **Given** an unknown symbol, **When** the endpoint is called, **Then** a 400 error is returned listing supported symbols
- [ ] **Given** a backtest completes with trades, **Then** the result includes summary metrics and a full trades array
- [ ] **Given** a backtest completes with no trades, **Then** the trades array is empty and summary metrics are zero
- [ ] **Given** the same inputs, **When** the endpoint is called twice, **Then** identical results are returned (deterministic)
- [ ] **Given** a persisted backtest result, **When** `GET /api/backtests/{id}` is called with its ID, **Then** the full result is returned with 200 status
- [ ] **Given** a non-existent backtest ID, **When** `GET /api/backtests/{id}` is called, **Then** a 404 error is returned
- [ ] **Given** valid symbol and intervals, **When** `GET /api/backtests/validate` is called, **Then** a coverage report is returned showing available date ranges and candle counts per interval
- [ ] **Given** no candle data exists for a symbol/interval pair, **When** the validate endpoint is called, **Then** that interval shows null dates and zero candle count
- [ ] **Given** a client disconnects during backtest execution, **When** the CancellationToken fires, **Then** the backtest is cancelled and no result is persisted
- [ ] **Given** a backtest exceeds the server-side timeout (5 minutes), **When** the timeout fires, **Then** the request returns 408 with a timeout message

## Objectives

- Create the `BacktestRun` domain entity and persistence layer for storing backtest results
- Create CQRS commands and queries for running backtests, retrieving results, and validating coverage
- Create the `BacktestsController` with three endpoints: POST (run), GET by ID (retrieve), GET validate (coverage)
- Ensure all validation rules from the PBI are enforced with appropriate HTTP status codes
- Support cancellation via CancellationToken and configurable server-side timeout

### Discovery References

- F3 (Backtest Replay Engine) is not yet implemented — `IBacktestRunner` interface exists but has no concrete implementation
- `BacktestConfig`, `BacktestResult`, `BacktestTrade`, `FeeModel`, `EquitySnapshot` models already exist in `TradingApp.Application/Backtesting/Models/`
- The `ICandleRepository.GetCandlesAsync()` method supports filtering by source and date range
- MediatR auto-discovers handlers from the `TradingApp.Application` assembly — no additional DI wiring needed for new handlers
- The `HttpGlobalExceptionFilter` maps `DomainException` → 400 and `NotFoundException` → 404 but does not handle `OperationCanceledException` → 408
- `BacktestConfig` uses Unix ms timestamps (`long`), not ISO 8601 strings
- `BacktestConfig` requires `InitialCapital` — to be added to the API request per user confirmation
- `BacktestConfig` uses `StrategyConfigJson` (string) — the API accepts a strongly-typed `GridStrategyConfig` DTO and serializes to JSON

### Project Patterns

- `src/TradingApp.Domain/Entities/Candle.cs` — Domain entity pattern (sealed class, private constructor, static `Create()` factory, private setters)
- `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` — Repository interface pattern
- `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — Repository implementation pattern (LINQ reads, EF Core context injection)
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — DbContext with DbSet registration, decimal→double conversion for SQLite
- `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` — DI registration entry point
- `src/TradingApp.Application/Abstractions/Commands/Command.cs` — CQRS command base types (`Command<T>`)
- `src/TradingApp.Application/Abstractions/Queries/Query.cs` — CQRS query base type (`Query<T>`)
- `src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs` — CQRS command handler base
- `src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs` — CQRS query handler base
- `src/TradingApp.Application/Candles/IngestCandlesCommand.cs` — Reference CQRS command + handler pattern
- `src/TradingApp.Api/Infrastructure/ApiController.cs` — Base controller with IMediator + IdentityService
- `src/TradingApp.Api/Infrastructure/Envelope.cs` — Error-only response wrapper
- `src/TradingApp.Api/Controllers/CandlesController.cs` — Reference controller (POST + validation + MediatR)
- `src/TradingApp.Api/Controllers/FundingRatesController.cs` — Reference controller (POST + validation)
- `src/TradingApp.Api/Models/IngestCandlesRequest.cs` — Request model with data annotations
- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Global exception → HTTP mapping
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — Controller integration test base
- `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — Controller test pattern (mock injection, status assertions)
- `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — Repository test pattern (SQLite in-memory, two-context verify)

### [x] Phase 1: Domain Entity & Persistence

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create `BacktestRun` domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-11-create-backtestrun-domain-entity

- [x] Task 1.2: Create `IBacktestRunRepository` interface
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-12-create-ibacktestrunrepository-interface

- [x] Task 1.3: Create `BacktestRunRepository` implementation
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-13-create-backtestrunrepository-implementation

- [x] Task 1.4: Update `TradingAppDbContext` with `BacktestRuns` DbSet
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-14-update-tradingappdbcontext-with-backtestruns-dbset

- [x] Task 1.5: Create EF Core migration
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-15-create-ef-core-migration

- [x] Task 1.6: Register repository in DI
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-16-register-repository-in-di

- [x] Task 1.7: Write `BacktestRunRepositoryTests`
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-17-write-backtestrunrepositorytests

- [x] Task 1.8: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-01-details.md#task-18-build-solution-and-run-all-tests

### [x] Phase 2: Application Layer — DTOs & CQRS Commands/Queries

**Complexity**: Medium | **Risk**: Medium

> **Prerequisite**: `BacktestResult` in `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` does not currently have a `CandlesReplayed` property. Task 2.3 must add `public required int CandlesReplayed { get; init; }` to `BacktestResult` before the handler code will compile.

- [x] Task 2.1: Create `GridStrategyConfig` and `BacktestRunResponse` DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-21-create-gridstrategyconfig-and-backtestrunresponse-dtos

- [x] Task 2.2: Create `CandleCoverageResponse` DTO
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-22-create-candlecoverageresponse-dto

- [x] Task 2.3: Create `RunBacktestCommand` and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-23-create-runbacktestcommand-and-handler

- [x] Task 2.4: Create `GetBacktestResultQuery` and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-24-create-getbacktestresultquery-and-handler

- [x] Task 2.5: Create `GetCandleCoverageQuery` and handler
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-25-create-getcandlecoveragequery-and-handler

- [x] Task 2.6: Add `OperationCanceledException` handling to `HttpGlobalExceptionFilter`
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-26-add-operationcanceledexception-handling-to-httpglobalexceptionfilter

- [x] Task 2.7: Build solution successfully
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-02-details.md#task-27-build-solution-successfully

### [x] Phase 3: API Controller & Integration Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create `RunBacktestRequest` API model
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-03-details.md#task-31-create-runbacktestrequest-api-model

- [x] Task 3.2: Create `BacktestsController`
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-03-details.md#task-32-create-backtestscontroller

- [x] Task 3.3: Write `BacktestsControllerTests` — happy paths
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-03-details.md#task-33-write-backtestscontrollertests-happy-paths

- [x] Task 3.4: Write `BacktestsControllerTests` — validation and error paths
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-03-details.md#task-34-write-backtestscontrollertests-validation-and-error-paths

- [x] Task 3.5: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260328-backtest-api-results-phase-03-details.md#task-35-build-solution-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Domain Entity & Persistence | Medium | Low |
| Phase 2: Application Layer — DTOs & CQRS | Medium | Medium |
| Phase 3: API Controller & Integration Tests | Medium | Low |
| **Total** | **Medium** | **Low-Medium** |

### Scoping Notes

- F3 (BacktestRunner) is not implemented — all handler tests mock `IBacktestRunner` via controller integration tests
- Command/Query handlers are tested indirectly via controller integration tests per project testing standards
- The `BacktestRun` entity uses a single flat table with JSON blobs for trade log and strategy config (per PBI spec)
- `InitialCapital` is added to the API request per user confirmation
- Timestamps are stored as Unix ms (`long`) in the entity following existing Candle pattern, converted to ISO 8601 in API responses
- The `GridStrategyConfig` DTO is strongly-typed at the API layer and serialized to `StrategyConfigJson` for `BacktestConfig`
- PBI acceptance criterion "no candle data → 404" is satisfied by `IBacktestRunner` throwing `NotFoundException` when no candles exist for the requested range — a corresponding mock test in Task 3.4 verifies the 404 response
- PBI acceptance criteria for cancellation (client disconnect) and timeout (5 min) are verified via a Task 3.4 test that mocks `IBacktestRunner` throwing `OperationCanceledException` and asserts 408 response

## Dependencies

- `IBacktestRunner` interface (exists in `TradingApp.Application.Abstractions.Services`)
- `BacktestConfig`, `BacktestResult`, `BacktestTrade`, `FeeModel` models (exist in `TradingApp.Application.Backtesting.Models`)
- `ICandleRepository` (exists in `TradingApp.Application.Abstractions.Repositories`)
- `System.Text.Json` for JSON serialization of trade log and strategy config blobs
- EF Core SQLite provider (already configured)
- MediatR (already configured with assembly scanning)

## Success Criteria

- All three API endpoints (`POST /api/backtests`, `GET /api/backtests/{id}`, `GET /api/backtests/validate`) return correct responses
- Backtest results are persisted to SQLite and retrievable by ID
- All validation rules from the PBI produce correct HTTP status codes (400, 404, 408)
- Controller integration tests verify happy paths and error paths
- Repository tests verify persistence and retrieval
- Solution builds cleanly with all existing and new tests passing

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-28T14:08:05Z | 2026-03-28T14:15:00Z |
| Plan Reviewer | plan-reviewed | 2026-03-28T15:30:00Z | 2026-03-28T14:33:33Z |
| Plan Implementer | implemented | 2026-03-28T14:45:51Z | 2026-03-28T15:30:58Z |
| Implementation Reviewer | complete | 2026-03-28T15:33:24Z | 2026-03-28T16:07:40Z |
