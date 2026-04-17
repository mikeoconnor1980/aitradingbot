---
applyTo: ".agent-context/3-develop/build/changes/20260324-f1-configuration-connectivity-changes.md"
currentAgent: ""
agentStartedAt: ""
status: "complete"
lastUpdated: "2026-03-24T21:25:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F1 — Configuration & Connectivity

## Overview

Scaffold the full enterprise .NET solution with MediatR, base classes, envelope pattern, and test infrastructure. Then implement Hyperliquid testnet wallet configuration, key derivation, and connectivity verification via a health check endpoint. Finally, build an Angular 19 standalone status card with auto-polling.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**PRD:** hyperliquid-poc-prd.md
**Implementation Phase:** 1 (Foundation)
**Risk Level:** Low
**Depends On:** None

### Summary

Configure a Hyperliquid testnet wallet and verify end-to-end connectivity from .NET backend through to the Angular frontend. The health endpoint focuses exclusively on Hyperliquid testnet reachability — no other infrastructure checks are in scope. F1 also establishes the full enterprise solution structure with MediatR CQRS, base classes, envelope responses, and test infrastructure that all subsequent features build on.

### Acceptance Criteria

- [ ] **Given** a valid private key in config, **When** the application starts, **Then** the wallet address is derived correctly and logged on startup
- [ ] **Given** no private key in config, **When** the application starts, **Then** it throws a clear startup error indicating the missing configuration
- [ ] **Given** a malformed private key in config, **When** the application starts, **Then** it throws a clear startup error with format guidance
- [ ] **Given** a running backend with valid config, **When** `GET /api/health` is called, **Then** it returns structured JSON with `status`, `walletAddress`, `network`, `timestamp`, and `error` fields
- [ ] **Given** the Hyperliquid testnet is reachable, **When** `GET /api/health` is called, **Then** `status` is `"connected"` and `error` is `null`
- [ ] **Given** the Hyperliquid testnet is unreachable, **When** `GET /api/health` is called, **Then** `status` is `"disconnected"` and `error` contains a descriptive message
- [ ] **Given** the Angular UI is loaded, **When** the page renders, **Then** a status card displays the connection status (green/red), truncated wallet address, and network name
- [ ] **Given** the Angular UI is loaded, **When** 10 seconds elapse, **Then** the health endpoint is polled automatically and the status card updates
- [ ] **Given** the Angular UI is loaded, **When** the user clicks the refresh button, **Then** the health endpoint is called immediately and the status card updates
- [ ] **Given** the project repository, **When** checking `.gitignore`, **Then** `appsettings.Development.json` is excluded from version control

## Objectives

- Scaffold the full enterprise solution (TradePilot.sln) with all 6 projects: Domain, Application, Infrastructure, Persistence, Api, Worker
- Establish MediatR CQRS base classes (Command, Query, Handler bases)
- Create ApiController base with IMediator, Envelope and CreatedResultEnvelope response wrappers
- Build test infrastructure: BaseControllerTests, FakeHttpMessageHandler, global Usings.cs
- Implement Hyperliquid configuration, key derivation, and connectivity verification
- Create a health check query/handler using MediatR, dispatched from HealthController
- Build an Angular 19 standalone status card with auto-polling and manual refresh

### Discovery References

- **Greenfield project**: No source code exists — zero `.cs`, `.csproj`, `.sln`, `.ts`, `angular.json` files
- **POC scope**: .NET 8, Angular 19 standalone, Nethereum for signing, no database, no Docker
- **Hyperliquid API**: POST `/info` with `{"type": "meta"}` is the unauthenticated connectivity check
- **Enterprise foundation**: All 6 solution projects scaffolded, MediatR with command/query handler base classes, ApiController base, Envelope pattern, BaseControllerTests with full test infrastructure
- **Angular instructions note**: `.github/instructions/angular.instructions.md` targets a different project (DTS.UKCT.Efiling with NgModules). This POC uses standalone components per Angular 19 defaults. DDX/DDS design system references are not applicable.
- **.gitignore gap**: `appsettings.Development.json` is not currently excluded — must be added
- **Testing**: MSTest + FluentAssertions (≤v6) + Moq per testing instructions

### Project Patterns

- `.agent-context/0-knowledge/06-project-structure.md` — Enterprise solution structure (TradePilot.sln with 6 projects)
- `.agent-context/3-develop/backlog/hyperlink-poc.md` — POC feature list and component mapping
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Hyperliquid REST API, wallet-based signing
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular: standalone, strict typing, service-based
- `.github/instructions/csharp.instructions.md` — sealed classes, IOptions, static factory, async/await, CancellationToken
- `.github/instructions/dotnet-architecture.instructions.md` — CQRS commands/queries, handler base classes, bounded context folders
- `.github/instructions/api-controllers.instructions.md` — ApiController base, MediatR dispatch, Envelope, ProducesResponseType
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤v6, Given_When_Then, BaseControllerTests, builder pattern

### [x] Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure

**Complexity**: High | **Risk**: Low

- [x] Task 1.1: Create solution and all project scaffolding
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-11-create-solution-and-all-project-scaffolding

- [x] Task 1.2: Create CQRS base records and handler base classes
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-12-create-cqrs-base-records-and-handler-base-classes

- [x] Task 1.3: Create Envelope and CreatedResultEnvelope response wrappers
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-13-create-envelope-and-createdresultenvelope-response-wrappers

- [x] Task 1.4: Create ApiController base class
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-14-create-apicontroller-base-class

- [x] Task 1.5: Configure MediatR and Program.cs shell
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-15-configure-mediatr-and-programcs-shell

- [x] Task 1.6: Create test projects with global usings and test infrastructure
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-16-create-test-projects-with-global-usings-and-test-infrastructure

- [x] Task 1.7: Build solution and verify scaffolding
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-01-details.md#task-17-build-solution-and-verify-scaffolding

### [x] Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Implement HyperliquidOptions configuration model
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-21-implement-hyperliquidoptions-configuration-model

- [x] Task 2.2: Implement HyperliquidSigner with Nethereum key derivation
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-22-implement-hyperliquidsigner-with-nethereum-key-derivation

- [x] Task 2.3: Implement HyperliquidRestClient for connectivity check
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-23-implement-hyperliquidrestclient-for-connectivity-check

- [x] Task 2.4: Create GetHealthQuery and handler using MediatR
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-24-create-gethealthquery-and-handler-using-mediatr

- [x] Task 2.5: Create HealthController using ApiController base
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-25-create-healthcontroller-using-apicontroller-base

- [x] Task 2.6: Configure Program.cs — DI, config validation, CORS, fail-fast
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-26-configure-programcs--di-config-validation-cors-fail-fast

- [x] Task 2.7: Update .gitignore and create appsettings files
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-27-update-gitignore-and-create-appsettings-files

- [x] Task 2.8: Write unit tests — signer and controller
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-28-write-unit-tests--signer-and-controller

- [x] Task 2.9: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-02-details.md#task-29-build-solution-and-run-all-tests

### [x] Phase 3: Frontend — Angular App, Service, Status Card

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Scaffold Angular 19 standalone application
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-03-details.md#task-31-scaffold-angular-19-standalone-application

- [x] Task 3.2: Create health response model and health API service
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-03-details.md#task-32-create-health-response-model-and-health-api-service

- [x] Task 3.3: Create status card component
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-03-details.md#task-33-create-status-card-component

- [x] Task 3.4: Wire status card into app and configure API proxy
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-03-details.md#task-34-wire-status-card-into-app-and-configure-api-proxy

- [x] Task 3.5: Build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260324-f1-configuration-connectivity-phase-03-details.md#task-35-build-and-lint-verification

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure | High | Low |
| Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests | Medium | Low |
| Phase 3: Frontend — Angular App, Service, Status Card | Medium | Low |
| **Total** | **Medium-High** | **Low** |

### Scoping Notes

- All 6 solution projects are scaffolded in Phase 1 — Domain, Application, Persistence, and Worker will be mostly empty shells with base classes only
- MediatR CQRS pattern is established in Phase 1 and used for the health endpoint in Phase 2
- ApiController base class includes IMediator — controllers dispatch to handlers, never contain business logic
- Envelope pattern provides consistent error response format across all endpoints
- BaseControllerTests provides integration-style controller testing per the testing instructions
- Angular frontend uses standalone components (Angular 19), not NgModules from enterprise instruction files
- DDX/DDS design system from instruction files is not applicable to this POC — using plain SCSS
- FluentAssertions ≤v6 per licensing requirement in testing.instructions.md
- IdentityService in ApiController base will be a stub/placeholder — no auth in this POC
- No architecture tests (Arch.Tests) in this phase — will be added when the codebase has enough layers to validate

## Dependencies

- **Nethereum.Signer** NuGet package — Ethereum-compatible wallet key derivation
- **MediatR** NuGet package — CQRS command/query dispatch
- **FluentAssertions** NuGet package (≤v6) — test assertions
- **Moq** NuGet package — mocking for tests
- **Angular CLI** (v19) — frontend scaffolding
- **Node.js / npm** — Angular build tooling
- **.NET 8 SDK** — backend runtime

## Success Criteria

- `dotnet build TradePilot.sln` succeeds with all 6 projects + test projects
- MediatR base classes (Command, Query, Handler bases) exist and compile
- ApiController base, Envelope, CreatedResultEnvelope compile and work
- BaseControllerTests infrastructure compiles
- `dotnet test TradePilot.sln` passes all unit tests (signer derivation, config validation, health controller)
- Backend starts with valid config and logs the derived wallet address
- Backend fails fast with clear error when private key is missing or malformed
- `GET /api/health` returns structured JSON dispatched through MediatR
- Angular app builds and lints cleanly
- Status card displays connection status, truncated wallet address, network name
- Status card auto-polls every 10 seconds and supports manual refresh
- `appsettings.Development.json` is excluded from git

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| 3-Develop: 3 Reviewer | Completed | 2026-03-24T21:03:42Z | 2026-03-24T21:25:00Z |
| 3-Develop: 2 Implementer | Completed | 2026-03-24T21:00:00Z | 2026-03-24T21:30:00Z |
| Implementation Planner | planned | 2026-03-24T19:15:46Z | 2026-03-24T20:00:30Z |
| Plan Reviewer | plan-reviewed | 2026-03-24T20:08:38Z | 2026-03-24T20:29:15Z |
| Implementation Planner | planned | 2026-03-24T19:15:46Z | 2026-03-24T20:00:30Z |
