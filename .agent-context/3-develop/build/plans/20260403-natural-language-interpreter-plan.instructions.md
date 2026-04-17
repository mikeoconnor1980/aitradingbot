---
applyTo: ".agent-context/3-develop/build/changes/20260403-natural-language-interpreter-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T20:10:56Z"
status: "complete"
lastUpdated: "2026-04-03T20:50:31Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F9 — Natural Language Strategy Interpreter

## Overview

Enable traders to describe strategies in plain English via a text input in the Strategy Builder UI. An LLM interprets the text and maps it directly to the canonical `StrategyConfig` schema, returning a `StrategyIntentDto` with config, confidence, assumptions. Uses Gemini 2.0 Flash (primary) and Ollama (fallback) via OpenAI-compatible API.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Reference:** [F9-natural-language-interpreter.md](../../backlog/draft/strategy-input/F9-natural-language-interpreter.md)

### User Story

> As a **trader**, I want to **describe my strategy in plain text and see the generated configuration in the form builder** so that **I can quickly create strategies and fine-tune the details**.

### Acceptance Criteria

#### Backend

- Given "Buy ETH when RSI drops below 30 with 2% take profit", When interpreted, Then returns signal mode config with RSI condition (period 14, operator lt, threshold 30) and TP 2%
- Given "Set up a 5-level grid on BTC with 0.5% spacing", When interpreted, Then returns grid mode config with gridLevels 5, gridSpacing 0.5
- Given ambiguous text "trade BTC", When interpreted, Then confidence < 0.5 and `clarificationNeeded` populated
- Given text referencing unsupported indicator "Ichimoku cloud", When interpreted, Then `clarificationNeeded` explains this condition type is not yet supported
- Given 11th request within 1 minute, When submitted, Then HTTP 429 returned
- Given LLM unavailable, When interpreted, Then appropriate error response, no unhandled exception
- Given empty or whitespace-only text, When submitted, Then HTTP 400 returned
- Given strategy saved after NL generation, When config persisted, Then `sourceText` field contains original NL input

#### UI

- Given user types strategy NL text, When Generate clicked, Then form populated with interpreted config and assumptions shown
- Given interpretation returns confidence 0.4, When displayed, Then red badge and warning message visible, save still allowed
- Given assumption displayed, When "Edit" clicked, Then view scrolls to relevant field in the form
- Given form already has values, When user re-generates, Then confirmation dialog shown before overwriting
- Given interpreter returns error (rate limit), When displayed, Then error message shown, form unchanged
- Given editing a strategy with saved sourceText, When Strategy Builder opens, Then NL text area pre-loaded with saved text
- Given user modifies saved NL text and re-interprets, When result returned, Then changes vs. current form highlighted before applying

## Objectives

- Create TradePilot.AI project with LLM client infrastructure (OpenAI-compatible HTTP client)
- Implement strategy interpreter service with prompt engineering for NL → StrategyConfig mapping
- Add interpret API endpoint with rate limiting and input validation
- Build frontend NL input UI with assumptions display, confidence badge, and form population
- Support iteration: re-interpret, source text persistence, confirmation dialogs

### Discovery References

- `StrategyEntryPoint.NaturalLanguage` enum value already exists in `SourceMetadata`
- `SourceMetadata` has `EntryPoint` + `Summary` fields — add `SourceText` for original NL input
- `StrategyConfig` uses `StrategyJsonOptions.Default` for serialization — LLM output must use same options
- `RateLimitException` → 429 mapping already exists in `HttpGlobalExceptionFilter`
- No inbound rate limiting middleware exists — use ASP.NET Core built-in `AddRateLimiter`
- Frontend form population via `patchValue()` and `ConditionFactoryService` for conditions FormArray
- `SKIP_ERROR_NOTIFICATION` HttpContext token available for in-component error display

### Project Patterns

- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — IOptions configuration pattern
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — typed HTTP client interface
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — typed HTTP client implementation
- `src/TradePilot.Application/StrategyAuthoring/Commands/CreateStrategyCommand.cs` — MediatR command + handler pattern
- `src/TradePilot.Application/StrategyAuthoring/Models/StrategyConfig.cs` — canonical config schema
- `src/TradePilot.Application/StrategyAuthoring/Models/SourceMetadata.cs` — source tracking record
- `src/TradePilot.Api/Controllers/StrategiesController.cs` — controller with CRUD pattern
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — exception to HTTP mapping
- `src/TradePilot.Persistence/PersistenceServiceExtensions.cs` — DI extension method pattern
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — controller test base class
- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — unit test pattern
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — form builder
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — API service pattern

### [x] Phase 1: LLM Client Infrastructure

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create TradePilot.AI project and add to solution
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-11-create-TradePilot-ai-project
- [x] Task 1.2: Create LlmOptions configuration class
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-12-create-llmoptions-configuration
- [x] Task 1.3: Create ILlmClient interface and OpenAI-compatible implementation
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-13-create-illmclient-and-implementation
- [x] Task 1.4: Create AiServiceExtensions for DI registration
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-14-create-aiserviceextensions
- [x] Task 1.5: Add LLM configuration to appsettings.json and wire in Program.cs
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-15-add-configuration-and-wire-programcs
- [x] Task 1.6: Add unit tests for LLM client
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-16-add-unit-tests
- [x] Task 1.7: Build verification and architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-01-details.md#task-17-build-verification

### [x] Phase 2: Strategy Interpreter Service

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Create StrategyIntentDto and Assumption model
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-21-create-strategyintentdto
- [x] Task 2.2: Add SourceText field to SourceMetadata
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-22-add-sourcetext-to-sourcemetadata
- [x] Task 2.3: Create IStrategyInterpreter interface and implementation with prompt engineering
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-23-create-strategy-interpreter
- [x] Task 2.4: Create InterpretStrategyCommand and handler
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-24-create-interpretstrategycommand
- [x] Task 2.5: Register interpreter in DI
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-25-register-interpreter-in-di
- [x] Task 2.6: Add unit tests for interpreter and command handler
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-26-add-unit-tests
- [x] Task 2.7: Build verification and architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-02-details.md#task-27-build-verification

### [x] Phase 3: API Endpoint and Rate Limiting

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create InterpretStrategyRequest DTO with validation
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-03-details.md#task-31-create-request-dto
- [x] Task 3.2: Add interpret endpoint to StrategiesController
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-03-details.md#task-32-add-interpret-endpoint
- [x] Task 3.3: Configure ASP.NET Core rate limiting
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-03-details.md#task-33-configure-rate-limiting
- [x] Task 3.4: Add controller integration tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-03-details.md#task-34-add-integration-tests
- [x] Task 3.5: Build verification and architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-03-details.md#task-35-build-verification

### [x] Phase 4: Frontend — NL Interpretation UI

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Add NL interpretation models and API service method
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-41-add-frontend-models-and-service
- [x] Task 4.2: Create NL input card component
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-42-create-nl-input-card
- [x] Task 4.3: Create assumptions panel component
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-43-create-assumptions-panel
- [x] Task 4.4: Create confidence badge component
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-44-create-confidence-badge
- [x] Task 4.5: Integrate NL components into Strategy Builder page
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-45-integrate-into-strategy-builder
- [x] Task 4.6: Implement form population from interpreter result
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-46-implement-form-population
- [x] Task 4.7: Implement re-interpret flow and source text persistence
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-47-reinterpret-and-source-text
- [x] Task 4.8: Add Angular tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-48-add-angular-tests
- [x] Task 4.9: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260403-natural-language-interpreter-phase-04-details.md#task-49-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: LLM Client Infrastructure | Medium | Low |
| Phase 2: Strategy Interpreter Service | High | Medium |
| Phase 3: API Endpoint and Rate Limiting | Medium | Low |
| Phase 4: Frontend — NL Interpretation UI | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- LLM prompt engineering quality is the primary risk — confidence scoring and assumption extraction depend on LLM output consistency
- Gemini 2.0 Flash and Ollama both expose OpenAI-compatible APIs — single HTTP client implementation with configurable base URL
- No Docker/CI changes required — new project is added to solution only
- Rate limiting uses ASP.NET Core built-in middleware — no third-party packages
- `StrategyEntryPoint.NaturalLanguage` and `SourceMetadata` already exist — minimal domain model changes
- Frontend leverages existing `patchValue()` and `ConditionFactoryService` patterns for form population

## Dependencies

- Google Gemini API key (for primary LLM provider)
- Ollama installed locally (for offline fallback — optional)
- No new NuGet packages required (uses built-in HttpClient + System.Text.Json)

## Success Criteria

- Trader can type a strategy description and receive a populated Strategy Builder form
- Confidence and assumptions are clearly displayed
- Low-confidence or ambiguous inputs are flagged with clarification
- Rate limiting prevents API abuse (10 req/min/IP)
- Source text is persisted on saved strategies and pre-loaded on edit
- All backend tests pass; frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-03T17:42:37Z | 2026-04-03T18:22:58Z |
| Plan Reviewer | reviewed | 2026-04-03T18:25:23Z | 2026-04-03T18:30:00Z |
| Plan Implementer | implemented | 2026-04-03T18:37:28Z | 2026-04-03T19:09:25Z |
| Implementation Reviewer | complete | 2026-04-03T20:10:56Z | 2026-04-03T20:50:31Z |
