<!-- markdownlint-disable-file -->
# Release Changes: AI Strategy Review

**Related Plan**: 20260404-ai-strategy-review-plan.instructions.md
**Implementation Date**: 2026-04-04

## Summary

Implemented the AI Strategy Review feature across domain, AI services, API endpoints, and Angular UI. The system now persists revision-linked reviews, uses an independently configured review LLM client, exposes review endpoints with throttling, and renders saved reviews in the strategy builder with cooldown-aware UI actions.

## Changes

### Added

<!-- Phase 1: Domain, Persistence & Configuration -->
- src/TradingApp.Domain/Entities/StrategyReview.cs: Added the new immutable review entity with validated factory creation.
- src/TradingApp.Application/Abstractions/Repositories/IStrategyReviewRepository.cs: Added the application-layer repository contract for strategy reviews.
- src/TradingApp.Persistence/Repositories/StrategyReviewRepository.cs: Added the EF Core repository implementation for add, lookup, and overwrite support.
- src/TradingApp.Application/Abstractions/Configuration/LlmReviewOptions.cs: Added typed options for the independent LlmReview configuration section.
- src/TradingApp.Persistence/Migrations/20260404082527_AddStrategyReviews.cs: Added the EF Core migration creating the StrategyReviews table and unique composite index.
- src/TradingApp.Persistence/Migrations/20260404082527_AddStrategyReviews.Designer.cs: Added the generated migration designer metadata.
- tests/TradingApp.Domain.Tests/Entities/StrategyReviewTests.cs: Added unit tests covering valid creation and all entity guard clauses.

<!-- Phase 2: AI Service Layer -->
- src/TradingApp.AI/Prompts/StrategyReviewPrompt.cs: Added the server-side strategy review system prompt.
- src/TradingApp.Application/Abstractions/Services/IStrategyReviewer.cs: Added the application service contract for strategy reviews.
- src/TradingApp.Application/Abstractions/Services/IReviewLlmClient.cs: Added the marker interface for the review-specific LLM client registration.
- src/TradingApp.AI/Services/StrategyReviewer.cs: Added the AI review service that sends strategy JSON to the review LLM.
- src/TradingApp.AI/Services/ReviewLlmClient.cs: Added the independently configured OpenAI-compatible review client using LlmReviewOptions.
- src/TradingApp.Application/StrategyAuthoring/Commands/RequestStrategyReviewCommand.cs: Added the CQRS command and handler to validate ownership, overwrite any prior review, and persist a new review.
- src/TradingApp.Application/StrategyAuthoring/Queries/GetStrategyReviewQuery.cs: Added the CQRS query and handler to retrieve a persisted review with ownership checks.
- src/TradingApp.Application/StrategyAuthoring/Models/StrategyReviewDto.cs: Added the DTO returned by the command and query layer for strategy reviews.
- tests/TradingApp.AI.Tests/Services/StrategyReviewerTests.cs: Added AI service unit tests covering success, invalid input, and LLM failure behavior.

<!-- Phase 3: API Endpoints & Integration Tests -->
- tests/TradingApp.Api.Tests/Controllers/StrategyReviewTests.cs: Added integration coverage for review creation, retrieval, overwrite, missing-resource cases, invalid revision, and rate limiting.

<!-- Phase 4: Angular Frontend -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy-review.model.ts: Added the frontend DTO for persisted strategy review data.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.ts: Added the standalone collapsible review summary component logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.html: Added the expandable summary card template with disabled Apply Suggestions action.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.scss: Added styling for rendered markdown preview and card actions.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.ts: Added the standalone dialog component for full markdown review display.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.html: Added the modal template with review metadata and disabled placeholder action.
- frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.scss: Added modal layout and markdown presentation styles.

### Modified

<!-- Phase 1: Domain, Persistence & Configuration -->
- src/TradingApp.Persistence/TradingAppDbContext.cs: Added the StrategyReview DbSet and inline entity configuration with foreign key and unique index.
- src/TradingApp.Persistence/Migrations/TradingAppDbContextModelSnapshot.cs: Updated the EF model snapshot to include StrategyReview.
- src/TradingApp.Persistence/PersistenceServiceExtensions.cs: Registered IStrategyReviewRepository in dependency injection.
- src/TradingApp.AI/AiServiceExtensions.cs: Bound and validated LlmReviewOptions on startup.
- src/TradingApp.Api/appsettings.json: Added the LlmReview configuration section.
- src/TradingApp.Api/TradingApp.Api.csproj: Added Microsoft.EntityFrameworkCore.Design so the required API-startup migration workflow works.

<!-- Phase 2: AI Service Layer -->
- src/TradingApp.AI/AiServiceExtensions.cs: Registered the review-specific typed HttpClient and IStrategyReviewer service alongside the existing AI registrations.

<!-- Phase 3: API Endpoints & Integration Tests -->
- src/TradingApp.Api/Controllers/StrategiesController.cs: Added POST and GET review endpoints for strategy revisions with validation, MediatR dispatch, and 404 handling for missing reviews.
- src/TradingApp.Api/Program.cs: Added the review-specific fixed-window rate limiting policy at 1 request per minute per IP.
- tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs: Added default LlmReview startup settings for shared API integration test hosts.
- tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs: Added LlmReview startup settings for direct WebApplicationFactory-based API tests.
- tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs: Added LlmReview startup settings for direct WebApplicationFactory-based API tests.
- tests/TradingApp.Api.Tests/Hubs/MarketDataHubTests.cs: Added LlmReview startup settings for direct WebApplicationFactory-based hub tests.

<!-- Phase 4: Angular Frontend -->
- frontend/trading-ui/package.json: Added marked and @types/marked dependencies for markdown rendering.
- frontend/trading-ui/package-lock.json: Updated lockfile for the new markdown dependencies.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts: Added POST and GET review methods for strategy revisions.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Integrated review loading, review requests, modal opening, cooldown tracking, and cleanup.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Added the AI Review action button and rendered the review card in the side column.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.scss: Added layout styles for the new action button and responsive wrapper.

### Removed

<!-- Phase 1: Domain, Persistence & Configuration -->
- None.

<!-- Phase 2: AI Service Layer -->
- None.

<!-- Phase 3: API Endpoints & Integration Tests -->
- None.

<!-- Phase 4: Angular Frontend -->
- None.

## Test Results

<!-- Phase 1: Domain, Persistence & Configuration -->
- StrategyReviewTests: 10/10 passed.
- Solution Build: PASSED.
- Migration Apply Check: PASSED.
- Architecture Tests: Not run - not required by Phase 1.

<!-- Phase 2: AI Service Layer -->
- dotnet build: PASSED.
- StrategyReviewer filtered tests: 5/5 passed.
- TradingApp.AI.Tests: 9/9 passed.
- Architecture Tests: Not run - not required by Phase 2.

<!-- Phase 3: API Endpoints & Integration Tests -->
- dotnet build: PASSED.
- StrategyReviewTests: 8/8 passed.
- TradingApp.Domain.Tests: 56/56 passed.
- TradingApp.AI.Tests: 14/14 passed.
- TradingApp.Application.Tests: 220/220 passed.
- TradingApp.Api.Tests: 194/194 passed.
- TradingApp.Indicators.Tests: 33/33 passed.
- TradingApp.Infrastructure.Tests: 59/59 passed.
- TradingApp.Persistence.Tests: 28/28 passed.
- Architecture Tests: Not present in repository.

<!-- Phase 4: Angular Frontend -->
- Angular build (npm run build): PASSED.
- Angular lint (npm run lint): PASSED.
- Architecture Tests: Not run - not required by Phase 4.

## Issues

<!-- Phase 1: Domain, Persistence & Configuration -->
- dotnet ef migrations add initially failed because the API startup project did not reference Microsoft.EntityFrameworkCore.Design; resolved by adding the design package to TradingApp.Api.csproj.
- The first migration apply check failed because the SQLite path was relative to the API startup folder; resolved by rerunning dotnet ef database update with an absolute database path.

<!-- Phase 2: AI Service Layer -->
- The initial build failed because the new test referenced the internal StrategyReviewPrompt type directly; resolved by asserting prompt content via a string matcher instead of widening production visibility.
- The runTests tool did not resolve the single-file StrategyReviewer scope correctly, so the exact phase-specified dotnet test filter command was run to complete verification.

<!-- Phase 3: API Endpoints & Integration Tests -->
- Full API test execution initially failed because LlmReviewOptions validates on startup and existing API tests did not provide review-client settings; resolved by supplying LlmReview test settings in the shared API test base and the remaining direct WebApplicationFactory test hosts.
- The overwrite integration test would have been masked by the new 1-per-minute IP throttling; resolved by sending the overwrite requests with different X-Forwarded-For values so overwrite behavior and rate limiting are tested independently.

<!-- Phase 4: Angular Frontend -->
- Angular Material content projection rejected the initial conditional button markup for the AI Review icon and spinner state; resolved by simplifying the button template so the icon slot is projected correctly.
- The frontend build reports existing bundle and style budget warnings unrelated to this phase; the build still completed successfully.

## Design Decisions

<!-- Phase 1: Domain, Persistence & Configuration -->
- Added EF Core design tooling to the API host rather than changing the prescribed migration command because the phase explicitly requires generating the migration with --startup-project src/TradingApp.Api.
- Reused the existing partial Phase 1 work already present in the worktree and only filled the gaps needed to complete verification and migration generation.

<!-- Phase 2: AI Service Layer -->
- Reused the existing hidden-not-found ownership pattern in the new command and query handlers so unauthorized access and inactive strategies continue to surface as NotFoundException.
- Implemented ReviewLlmClient with the same response validation and HTTP error handling style as OpenAiCompatibleLlmClient, but bound to LlmReviewOptions so the review path stays independently configurable.

<!-- Phase 3: API Endpoints & Integration Tests -->
- Kept the production LlmReviewOptions startup validation intact and fixed test host configuration instead of weakening runtime validation semantics.
- Used per-request forwarded IP headers in review endpoint tests to isolate overwrite behavior from the stricter review throttling policy.

<!-- Phase 4: Angular Frontend -->
- Kept the AI Review button visible but disabled for unsaved strategies to satisfy the acceptance criterion, even though the phase detail text suggested showing it only for saved strategies.
- Used a review key based on strategy id plus revision number for the cooldown so the timer only applies to the currently loaded saved revision.
- Implemented the summary card with an expansion panel rather than a static card so the collapsible summary requirement is met directly.

## Review Hints

<!-- Phase 1: Domain, Persistence & Configuration -->
- Verify whether keeping Microsoft.EntityFrameworkCore.Design on the API startup project is the preferred long-term tooling approach for this repository, or whether future migrations should rely solely on the persistence design-time factory.

<!-- Phase 2: AI Service Layer -->
- Review the IReviewLlmClient DI registration path in the next API integration phase, because typed HttpClient registrations and test-time service replacement will need to coexist cleanly in controller tests.

<!-- Phase 3: API Endpoints & Integration Tests -->
- Review whether direct WebApplicationFactory<Program> usage in API tests should be consolidated behind a shared helper, because startup configuration like LlmReview now has to be kept in sync across multiple test entry points.

<!-- Phase 4: Angular Frontend -->
- Verify whether the product wants AI Review to remain available when the user has unsaved edits on an existing strategy, because the backend review operates on the saved revision currently loaded by the page.

## Release Summary

Implemented all four phases of AI Strategy Review. The backend now supports persisted review records per strategy revision, independent LLM review configuration, CQRS request and retrieval handlers, and throttled API endpoints. The frontend now exposes an AI Review action in the strategy builder, renders collapsible markdown summaries and full-review dialogs, enforces a per-revision cooldown, and shows a disabled Apply Suggestions placeholder. Verification completed with passing domain, AI, application, API, infrastructure, persistence, and frontend build and lint runs.
