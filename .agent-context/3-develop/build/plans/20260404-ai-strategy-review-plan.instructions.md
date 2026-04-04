---
applyTo: ".agent-context/3-develop/build/changes/20260404-ai-strategy-review-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-04T09:08:13Z"
status: "complete"
lastUpdated: "2026-04-04T09:38:38Z"
---
<!-- markdownlint-disable-file -->

# Task Checklist: AI Strategy Review

## Overview

Add an AI-powered strategy review feature that allows traders to get a structured LLM-based critical assessment of their trading strategy configuration, with reviews persisted per strategy revision and accessible from the strategy editor UI.

## PBI Details

As a trader, I want to run an AI review on my trading strategy JSON from the strategy screen so that I get a structured, critical assessment of my strategy's design, risks, and weaknesses before deploying it.

### Acceptance Criteria

- [ ] **Given** a saved strategy is open in the strategy editor, **When** the user clicks the "AI Review" button, **Then** the strategy JSON is sent to the backend and an AI review is returned and displayed below the editor as a collapsible markdown summary
- [ ] **Given** a review summary is displayed below the editor, **When** the user clicks "View Full Review", **Then** a centered modal opens showing the full rendered markdown review
- [ ] **Given** the user has just run an AI review, **When** they attempt to run another review within 1 minute for the same strategy, **Then** the button is disabled and shows a countdown timer
- [ ] **Given** a review is in progress, **When** the user observes the UI, **Then** a loading spinner is visible and the form remains interactive
- [ ] **Given** a strategy revision already has a review, **When** the user re-runs the review on the same revision, **Then** the previous review is overwritten with the new one
- [ ] **Given** a strategy has multiple revisions with reviews, **When** the user views a past revision, **Then** the linked review for that revision is displayed
- [ ] **Given** the LLM call fails or times out, **When** the error occurs, **Then** a user-friendly error message is shown and the user can retry
- [ ] **Given** the strategy has not been saved, **When** the user views the editor, **Then** the AI Review button is disabled with a tooltip indicating the strategy must be saved first
- [ ] **Given** the `LlmReview` configuration section is set in `appsettings.json`, **When** the application starts, **Then** the review LLM client uses that configuration independently from the `Llm` section
- [ ] **Given** a review is displayed (summary or modal), **When** the user sees the "Apply Suggestions" button, **Then** it is visually disabled (greyed out) with a "Coming Soon" tooltip and is not clickable

## Objectives

- Create a new `StrategyReview` domain entity linked to `StrategyRevision`
- Implement a second independently-configured LLM client for strategy review
- Add API endpoints for triggering and retrieving reviews
- Build Angular UI with collapsible summary, full-review modal, cooldown timer, and markdown rendering
- Provide comprehensive test coverage across all layers

### Discovery References

- `StrategyInterpreter` in `TradingApp.AI` is the exact pattern precedent for the new `StrategyReviewer` service
- `InterpretStrategyCommand` is the CQRS precedent for the new `RequestStrategyReviewCommand`
- `ConfirmDialogComponent` is the modal dialog pattern for the review modal
- `PreviewSummaryCardComponent` is the collapsible side-panel card pattern for the review summary
- `LlmOptions` + `AiServiceExtensions` define the configuration and DI registration pattern

### Project Patterns

- `src/TradingApp.Domain/Entities/StrategyRevision.cs` - Immutable revision entity with factory method and private constructor
- `src/TradingApp.AI/Services/StrategyInterpreter.cs` - AI service consuming ILlmClient with system prompt
- `src/TradingApp.AI/Prompts/StrategyInterpreterPrompt.cs` - Server-side prompt as static class
- `src/TradingApp.AI/AiServiceExtensions.cs` - DI registration for AI services and LLM options
- `src/TradingApp.Application/StrategyAuthoring/Commands/InterpretStrategyCommand.cs` - CQRS command delegating to AI service
- `src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyVersionsQuery.cs` - Query with ownership check and paged results
- `src/TradingApp.Api/Controllers/StrategiesController.cs` - Controller with MediatR dispatch and rate limiting
- `src/TradingApp.Persistence/TradingAppDbContext.cs` - Inline EF entity configuration in OnModelCreating
- `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` - Repository DI registration
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` - Strategy editor page
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` - Strategy API service
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` - Modal dialog pattern
- `tests/TradingApp.AI.Tests/Services/StrategyInterpreterTests.cs` - AI service unit test pattern
- `tests/TradingApp.Api.Tests/Controllers/InterpretStrategyTests.cs` - API integration test with LLM mock and rate limiting

### [x] Phase 1: Domain, Persistence & Configuration

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create StrategyReview domain entity
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-11-create-strategyreview-domain-entity

- [x] Task 1.2: Create IStrategyReviewRepository interface
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-12-create-istrategyreviewrepository-interface

- [x] Task 1.3: Create StrategyReviewRepository implementation
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-13-create-strategyreviewrepository-implementation

- [x] Task 1.4: Add DbContext configuration and migration
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-14-add-dbcontext-configuration-and-migration

- [x] Task 1.5: Create LlmReviewOptions configuration class
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-15-create-llmreviewoptions-configuration-class

- [x] Task 1.6: Register repository and configuration in DI
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-16-register-repository-and-configuration-in-di

- [x] Task 1.7: Add LlmReview section to appsettings.json
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-17-add-llmreview-section-to-appsettingsjson

- [x] Task 1.8: Write domain entity unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-18-write-domain-entity-unit-tests

- [x] Task 1.9: Build and run domain tests
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-01-details.md#task-19-build-and-run-domain-tests

### [x] Phase 2: AI Service Layer

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Create StrategyReviewPrompt static class
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-21-create-strategyreviewprompt-static-class

- [x] Task 2.2: Create IStrategyReviewer interface
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-22-create-istrategyreviewer-interface

- [x] Task 2.3: Create StrategyReviewer service implementation
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-23-create-strategyreviewer-service-implementation

- [x] Task 2.4: Register second LLM client and reviewer service in DI
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-24-register-second-llm-client-and-reviewer-service-in-di

- [x] Task 2.5: Create RequestStrategyReviewCommand and handler
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-25-create-requeststrategyreviewcommand-and-handler

- [x] Task 2.6: Create GetStrategyReviewQuery and handler
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-26-create-getstrategyreviewquery-and-handler

- [x] Task 2.7: Create StrategyReviewDto model
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-27-create-strategyreviewdto-model

- [x] Task 2.8: Write StrategyReviewer unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-28-write-strategyreviewer-unit-tests

- [x] Task 2.9: Build and run AI tests
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-02-details.md#task-29-build-and-run-ai-tests

### [x] Phase 3: API Endpoints & Integration Tests

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Add review endpoints to StrategiesController
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-03-details.md#task-31-add-review-endpoints-to-strategiescontroller

- [x] Task 3.2: Add review-strategy rate limiting policy
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-03-details.md#task-32-add-review-strategy-rate-limiting-policy

- [x] Task 3.3: Write API integration tests for review endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-03-details.md#task-33-write-api-integration-tests-for-review-endpoints

- [x] Task 3.4: Build and run all backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-03-details.md#task-34-build-and-run-all-backend-tests

### [x] Phase 4: Angular Frontend

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Install markdown rendering library
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-41-install-markdown-rendering-library

- [x] Task 4.2: Create AI review TypeScript models
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-42-create-ai-review-typescript-models

- [x] Task 4.3: Add review methods to StrategyApiService
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-43-add-review-methods-to-strategyapiservice

- [x] Task 4.4: Create AiReviewCardComponent (collapsible summary)
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-44-create-aireviewcardcomponent-collapsible-summary

- [x] Task 4.5: Create AiReviewModalComponent (full review modal)
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-45-create-aireviewmodalcomponent-full-review-modal

- [x] Task 4.6: Integrate AI Review button and card into strategy builder page
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-46-integrate-ai-review-button-and-card-into-strategy-builder-page

- [x] Task 4.7: Implement cooldown timer logic
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-47-implement-cooldown-timer-logic

- [x] Task 4.8: Build and lint frontend
  - Details: .agent-context/3-develop/build/plans/details/20260404-ai-strategy-review-phase-04-details.md#task-48-build-and-lint-frontend

> **Note**: Frontend component tests are deferred. UI behaviour is validated via backend integration tests and manual verification of acceptance criteria.

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain, Persistence & Configuration | Medium | Low |
| Phase 2: AI Service Layer | Medium | Medium |
| Phase 3: API Endpoints & Integration Tests | Medium | Low |
| Phase 4: Angular Frontend | High | Medium |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- The `StrategyReview` entity is a new aggregate linked to `StrategyRevision` via composite (StrategyId, RevisionNumber) with a unique index — not embedded on the revision itself
- The second LLM client uses a marker interface (`IReviewLlmClient`) with its own `AddHttpClient` registration to avoid keyed service complexity
- No markdown library exists in the frontend — `marked` will be installed as a new dependency
- The "Apply Suggestions" button is a disabled placeholder only; no implementation logic
- No streaming/SSE — entire LLM response returned at once
- Rate limiting at the API level (1 req/min per IP for review) supplements the frontend cooldown timer

## Dependencies

- `marked` npm package for frontend markdown rendering
- Existing `@angular/material` expansion panel module (`MatExpansionModule`)
- Existing `OpenAiCompatibleLlmClient` infrastructure in `TradingApp.AI`
- EF Core migrations tooling

## Success Criteria

- All acceptance criteria from the PBI are met
- All backend tests pass (domain, AI service, API integration)
- Frontend builds and lints successfully
- Review LLM is independently configured via `LlmReview` appsettings section
- Strategy reviews are persisted and retrievable per revision

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-04T07:54:46Z | 2026-04-04T07:54:46Z |
| Plan Reviewer | plan-reviewed | 2026-04-04T07:55:38Z | 2026-04-04T08:01:50Z |
| Plan Implementer | implemented | 2026-04-04T08:09:36Z | 2026-04-04T08:52:06Z |
| Implementation Reviewer | complete | 2026-04-04T09:08:13Z | 2026-04-04T09:38:38Z |
