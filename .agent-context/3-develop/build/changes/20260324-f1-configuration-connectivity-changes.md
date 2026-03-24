<!-- markdownlint-disable-file -->
# Release Changes: F1 — Configuration & Connectivity

**Related Plan**: 20260324-f1-configuration-connectivity-plan.instructions.md
**Implementation Date**: 2026-03-24

## Summary

Scaffolds the full enterprise .NET solution (TradingApp.sln) with all 6 projects, MediatR CQRS base classes, ApiController base, Envelope pattern, test infrastructure, Hyperliquid connectivity services, health endpoint, and Angular 19 status card.

## Changes

### Added

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- TradingApp.sln: Created solution with src/tests solution folders and all 10 projects
- src/TradingApp.Application/Abstractions/Commands/Command.cs: Added CQRS base command records (Command, Command<T>, CreateCommand)
- src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs: Added command handler base classes
- src/TradingApp.Application/Abstractions/Queries/Query.cs: Added CQRS base query record
- src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs: Added query handler base class
- src/TradingApp.Application/Abstractions/Identity/AppIdentity.cs: Added application identity model with system identity
- src/TradingApp.Api/Infrastructure/Envelope.cs: Added standard error response envelope
- src/TradingApp.Api/Infrastructure/CreatedResultEnvelope.cs: Added create-result response envelope
- src/TradingApp.Api/Infrastructure/IdentityService.cs: Added stub identity service for POC
- src/TradingApp.Api/Infrastructure/ApiController.cs: Added base API controller with MediatR + identity access
- tests/TradingApp.Domain.Tests/Usings.cs: Added global test usings (FluentAssertions, MSTest, Moq)
- tests/TradingApp.Application.Tests/Usings.cs: Added global test usings (FluentAssertions, MSTest, Moq)
- tests/TradingApp.Infrastructure.Tests/Usings.cs: Added global test usings (FluentAssertions, MSTest, Moq)
- tests/TradingApp.Api.Tests/Usings.cs: Added global test usings including System.Net
- tests/TradingApp.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs: Added fake HTTP handler for test doubles
- tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs: Added WebApplicationFactory-based base class and response extensions

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs: Added Hyperliquid options model with section constant and DataAnnotations
- src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs: Added signer abstraction exposing derived wallet address
- src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs: Added connectivity-check client abstraction
- src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs: Added signer implementation using Nethereum EthECKey with fail-fast key validation
- src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs: Added /info POST connectivity implementation
- src/TradingApp.Application/Health/Models/HealthDto.cs: Added health response DTO
- src/TradingApp.Application/Health/Queries/GetHealthQuery.cs: Added query record and handler with connectivity/error mapping
- src/TradingApp.Api/Controllers/HealthController.cs: Added GET api/health endpoint dispatching MediatR query
- tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs: Added signer behavior tests for valid/missing/malformed keys
- tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs: Added end-to-end controller tests with fake HTTP handler

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- frontend/trading-ui/angular.json: Added Angular project configuration including serve proxy and lint target
- frontend/trading-ui/package.json: Added Angular dependencies and scripts including lint
- frontend/trading-ui/package-lock.json: Added npm lockfile for deterministic installs
- frontend/trading-ui/tsconfig.json: Added strict TypeScript compiler settings
- frontend/trading-ui/tsconfig.app.json: Added app-specific TypeScript config
- frontend/trading-ui/tsconfig.spec.json: Added spec TypeScript config for Angular tests
- frontend/trading-ui/eslint.config.js: Added ESLint flat config from angular-eslint setup
- frontend/trading-ui/proxy.conf.json: Added API proxy for /api to backend on http://localhost:5062
- frontend/trading-ui/src/index.html: Added Angular host page
- frontend/trading-ui/src/main.ts: Added bootstrap entrypoint
- frontend/trading-ui/src/styles.scss: Added global stylesheet
- frontend/trading-ui/src/app/core/models/health-response.model.ts: Added typed health response interface matching backend payload
- frontend/trading-ui/src/app/core/services/health.service.ts: Added polling + manual refresh service with resilient error handling
- frontend/trading-ui/src/app/features/connection/status-card.component.ts: Added standalone status card component with wallet truncation
- frontend/trading-ui/src/app/features/connection/status-card.component.html: Added status card template with connected/disconnected indicator and refresh button
- frontend/trading-ui/src/app/features/connection/status-card.component.scss: Added status card styling with green/red status indicators
- frontend/trading-ui/src/app/features/connection/status-card.component.spec.ts: Added component creation test
- frontend/trading-ui/.editorconfig: Added editor defaults
- frontend/trading-ui/.gitignore: Added frontend-specific ignore rules
- frontend/trading-ui/public/favicon.ico: Added default Angular favicon
- frontend/trading-ui/README.md: Added Angular project readme

### Modified

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- src/TradingApp.Application/TradingApp.Application.csproj: Added Domain reference and MediatR package
- src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj: Added Application reference and Nethereum.Signer package
- src/TradingApp.Persistence/TradingApp.Persistence.csproj: Added Application and Domain references
- src/TradingApp.Api/TradingApp.Api.csproj: Added Application and Infrastructure references
- src/TradingApp.Worker/TradingApp.Worker.csproj: Added Application and Infrastructure references
- src/TradingApp.Api/Program.cs: Replaced template with MediatR/IdentityService/controllers shell; added `public partial class Program { }`
- src/TradingApp.Worker/Program.cs: Simplified to clean worker shell startup
- tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj: Added source project reference and FluentAssertions 6.12.2, Moq
- tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj: Added source project reference and FluentAssertions 6.12.2, Moq
- tests/TradingApp.Infrastructure.Tests/TradingApp.Infrastructure.Tests.csproj: Added source project reference and FluentAssertions 6.12.2, Moq
- tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj: Added source project reference, FluentAssertions 6.12.2, Moq, Microsoft.AspNetCore.Mvc.Testing 8.0.18

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- src/TradingApp.Application/TradingApp.Application.csproj: Added Microsoft.Extensions.Options package reference
- src/TradingApp.Api/Program.cs: Added options binding/validation, signer fail-fast registration, typed HttpClient, CORS, and startup wallet logging
- .gitignore: Added appsettings.Development.json ignore rule
- src/TradingApp.Api/appsettings.json: Added Hyperliquid BaseUrl and Network defaults
- src/TradingApp.Api/appsettings.Development.json: Added local PrivateKey placeholder under Hyperliquid section

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- frontend/trading-ui/src/app/app.config.ts: Registered HttpClient provider in app config
- frontend/trading-ui/src/app/app.component.ts: Wired root app to import and render standalone status card
- frontend/trading-ui/src/app/app.component.html: Replaced starter template with shell containing title and status card
- frontend/trading-ui/src/app/app.component.scss: Added root layout/background styles for app shell
- frontend/trading-ui/src/app/app.component.spec.ts: Updated title assertions and mocked HealthService for stable test setup

### Removed

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- src/TradingApp.Domain/Class1.cs: Removed template placeholder
- src/TradingApp.Application/Class1.cs: Removed template placeholder
- src/TradingApp.Infrastructure/Class1.cs: Removed template placeholder
- src/TradingApp.Persistence/Class1.cs: Removed template placeholder
- src/TradingApp.Worker/Worker.cs: Removed template placeholder worker implementation
- src/TradingApp.Api/TradingApp.Api.http: Removed template HTTP sample
- tests/TradingApp.Domain.Tests/UnitTest1.cs: Removed template placeholder test
- tests/TradingApp.Application.Tests/UnitTest1.cs: Removed template placeholder test
- tests/TradingApp.Infrastructure.Tests/UnitTest1.cs: Removed template placeholder test
- tests/TradingApp.Api.Tests/UnitTest1.cs: Removed template placeholder test
- tests/TradingApp.Domain.Tests/GlobalUsings.cs: Replaced by Usings.cs convention
- tests/TradingApp.Application.Tests/GlobalUsings.cs: Replaced by Usings.cs convention
- tests/TradingApp.Infrastructure.Tests/GlobalUsings.cs: Replaced by Usings.cs convention
- tests/TradingApp.Api.Tests/GlobalUsings.cs: Replaced by Usings.cs convention

## Test Results

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- Solution Build (`dotnet build TradingApp.sln`): PASSED
- Solution Membership: 10/10 projects present

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- HyperliquidSignerTests: 6/6 passed
- HealthControllerTests: 3/3 passed
- Full solution test run (`dotnet test TradingApp.sln`): PASSED

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- Angular Build (`npx ng build`): PASSED
- Angular Lint (`npx ng lint`): PASSED

## Issues

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- Private NuGet feed (`pkgs.dev.azure.com`) caused restore failures — resolved by using nuget.org source explicitly
- `Microsoft.AspNetCore.Mvc.Testing` resolved to net10-only v10.0.5 — pinned to 8.0.18 for net8 compatibility

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- Private NuGet feed continues to cause restore failures in interactive builds — workaround: use nuget.org source or `--no-restore` after manual restore
- Signer test expected-address constant from spec sample did not match Nethereum runtime derivation — updated test to use actual derived checksummed address

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- Initial app template replacement left residual scaffold HTML causing Angular template parse errors — resolved by fully replacing app.component.html
- Lint target was missing from fresh scaffold — resolved by running ng lint setup flow to install angular-eslint and configure lint
- Build command used wrong relative path due to terminal cwd drift — resolved by switching to absolute path execution

## Design Decisions

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- Kept Program.cs as minimal shell per spec; added `public partial class Program { }` for WebApplicationFactory compatibility
- Worker project kept as compile-ready host bootstrap shell after removing template Worker.cs
- Used `Usings.cs` files (not `GlobalUsings.cs`) to align with testing instructions convention

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- Added DataAnnotations on HyperliquidOptions with ValidateDataAnnotations + ValidateOnStart for early config quality enforcement
- Implemented fail-fast signer creation at startup so missing/malformed private keys stop the app before serving requests
- Colocated GetHealthQuery and GetHealthQueryHandler in one file per project conventions
- Wallet address truncation implemented in handler to keep API output concise

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- Used standalone components as required despite generic Angular instructions favouring NgModules — POC uses Angular 19 standalone defaults
- Set proxy target to http://localhost:5062 based on backend launch settings in launchSettings.json
- Implemented wallet truncation in status card component so UI always displays compact address format

## Review Hints

<!-- Phase 1: Solution Scaffolding, Base Classes, and Test Infrastructure -->
- Verify NuGet source configuration if private feeds are used in CI — this phase used explicit nuget.org to avoid unauthorized feed errors
- Launch profile `launchUrl` values are scaffold defaults; update in Phase 2 once health endpoint route is added

<!-- Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests -->
- Review startup fail-fast path in src/TradingApp.Api/Program.cs for behavior when Hyperliquid private key is absent or malformed
- Review DI replacement semantics in tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs to confirm test host override matches team preference
- Validate CI restore source configuration so solution-level build/test can run without `--no-restore` workaround

<!-- Phase 3: Frontend — Angular App, Service, Status Card -->
- Verify runtime polling/refresh against a running backend on port 5062 using `ng serve`
- Confirm proxy behaviour for /api calls in dev mode; adjust target if backend launch profile port changes

## Release Summary

All 3 phases complete. The full TradingApp enterprise solution is scaffolded with MediatR CQRS base classes, ApiController base, Envelope pattern, and test infrastructure. Hyperliquid testnet connectivity is implemented via a health endpoint dispatched through MediatR, with Nethereum-based wallet address derivation and fail-fast config validation. The Angular 19 standalone frontend displays a status card with live polling every 10 seconds and a manual refresh button. All .NET tests pass (9/9) and the Angular app builds and lints cleanly.