<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 1: Foundation & Architecture (00, 03, 06, 10)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation structure and content guidelines
- Every updated file must add a `## Future Recommendations` section at the end

### Task 1.1: Update `00-project-overview.md` {#task-11-update-project-overview}

Update the project overview to reflect the current state of the system.

- **Complexity**: Medium
- **Risk Factors**: None — overview changes
- **Files**:
  - `.agent-context/0-knowledge/00-project-overview.md` — update
- **Success**:
  - Business Model Option C is documented as the chosen model
  - `TradingApp.Indicators` is mentioned as a core project
  - Authentication system (JWT + Google SSO) is mentioned
  - Strategy Optimizer feature is mentioned
  - Macro Calendar feature is mentioned
  - Future Recommendations section added

#### Changes Required

1. **Business model decision**: Replace "No decision has been made" with a clear statement that Option C (Split Architecture) has been chosen. Add evidence: Worker is `TradingApp.ExecutionAgent` (Windows Service), API is control plane, private keys never on server.

2. **Add `TradingApp.Indicators` to project description**: A standalone library containing `AtrCalculator`, `BollingerBandsCalculator`, `EmaCalculator`, `MacdCalculator`, `RsiCalculator`, `SupportResistanceCalculator`, plus incremental variants.

3. **Add authentication**: Custom JWT authentication with email/password and Google OAuth is fully implemented. No Azure AD B2C or Auth0.

4. **Add Strategy Optimizer**: Parameter sweep + evolutionary optimizer with walk-forward OOS validation, Sharpe/Sortino/Calmar/Kelly metrics.

5. **Add Macro Calendar**: Economic calendar integration that blocks trading during high-impact event windows.

6. **Add Future Recommendations section**:
   - Admin dashboard and monitoring
   - Stripe/payment integration for subscription tiers
   - Additional strategy types (TrendBreakout, MeanReversion, FundingArbitrage)
   - Strategy marketplace / sharing
   - Mobile app

---

### Task 1.2: Update `03-infrastructure-architecture.md` {#task-12-update-infrastructure-architecture}

Realign infrastructure documentation with actual deployment.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/03-infrastructure-architecture.md` — update
- **Success**:
  - Phase 1 VPS framing removed or reframed
  - SQLite path convention updated
  - Azure SignalR documented (not Redis)
  - Bicep infrastructure documented
  - Conditional service registration documented
  - Future Recommendations section added

#### Changes Required

1. **Remove Phase 1 VPS docker-compose framing**: There is no `docker-compose.yml` in the repo. The only Docker artifact is `src/TradingApp.Api/Dockerfile`. Production deploys to Azure Container Apps via Bicep. Reframe as: "Development uses direct `dotnet run` + `ng serve`; production deploys to Azure."

2. **Fix SQLite paths**: Actual paths are `src/TradingApp.Api/Data/tradingapp.db` and `src/TradingApp.Worker/Data/tradingapp.db`, not `/data/sqlite/tradingapp.db`.

3. **Azure SignalR not Redis**: Replace "Redis backplane" mentions with Azure SignalR. `AzureSignalRPublisher.cs` in `TradingApp.Infrastructure/Services/` uses `Microsoft.Azure.SignalR.Management` for server-side push.

4. **Document Bicep infrastructure**: `infrastructure/main.bicep` provisions: Azure Container Apps environment, Azure SQL Server, Azure SignalR, Azure Static Web App, Log Analytics workspace.

5. **Add conditional service registration**: `MarketDataStreamService` and `UserEventStreamService` only start when `Azure:SignalR:ConnectionString` is configured. In non-Azure deployments, these don't run.

6. **Add `AzureSignalRPublisher`**: Documents `ISignalRPublisher` implementation for production push.

7. **Add `NetworkRoutingHandler` and `UserNetworkProvider`**: Per-user mainnet/testnet routing from `User.PreferredNetwork`.

8. **Key Vault gap**: Note that Azure Key Vault for secrets is not yet configured — JWT secret and DB credentials are Bicep parameters.

9. **Add Future Recommendations**:
   - Azure Key Vault integration for secrets management
   - Redis Cache for session state at scale
   - Horizontal scaling of Worker via Azure Container Apps Jobs
   - Observability (Application Insights, distributed tracing)

---

### Task 1.3: Update `06-project-structure.md` {#task-13-update-project-structure}

Add missing projects and components to the solution layout.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/06-project-structure.md` — update
- **Success**:
  - `TradingApp.Indicators` project added with description
  - Worker description corrected (client-side Windows Service `TradingApp.ExecutionAgent`)
  - All new Application layer folders documented
  - All new Infrastructure layer components documented
  - All new API controllers and services documented
  - All new frontend feature modules documented

#### Changes Required

1. **Add `TradingApp.Indicators`** to the project table:
   - Purpose: Standalone indicator calculation library (ATR, Bollinger, EMA, MACD, RSI, SupportResistance) with batch and incremental variants
   - Dependencies: No external NuGet; referenced by `TradingApp.Application`

2. **Fix `TradingApp.Worker` description**: It is `TradingApp.ExecutionAgent` (`AssemblyName=TradingApp.ExecutionAgent`, `SelfContained=true`, `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`). It's a client-side Windows Service deployed via InnoSetup installer.

3. **Add Application layer folders**:
   - `Agent/` — Control plane command store, heartbeat, command models
   - `Candles/` — Candle CRUD commands/queries
   - `FundingRates/` — Funding rate ingestion commands
   - `Help/` — Help queries
   - `LlmContextSnapshots/` — LLM snapshot queries/models
   - `MacroCalendar/` — Macro event services, configuration
   - `Optimization/` — Full parameter optimization system
   - `Subscriptions/` — Free tier subscription command
   - `Abstractions/Auth/` — Full auth abstraction (JWT, Google, password hashing)

4. **Add Infrastructure layer components**:
   - `AspNetPasswordHasher.cs`, `AzureSignalRPublisher.cs`, `GoogleTokenValidator.cs`
   - `HyperliquidAccountService.cs` (moved from API), `HyperliquidUserEventClient.cs`
   - `JwtTokenService.cs`, `LiveExecutionEngine.cs`, `MutableSignerProvider.cs`, `NonceProvider.cs`
   - `Providers/MacroCalendar/` — macro calendar provider

5. **Add API controllers**: `AgentController`, `AuthController`, `CandlesController`, `FundingRatesController`, `HelpController`, `LiveTradingController`, `MacroCalendarController`, `MarketContextController`, `OptimizationsController`, `OrdersController`, `ProfileController`, `RiskController`, `SubscriptionController`, `TradingController`, `WalletAddressController`, `WalletController`

6. **Add API infrastructure**: `CorrelationIdMiddleware.cs`, `NetworkRoutingHandler.cs`, `UserNetworkProvider.cs`

7. **Add API services**: `BacktestProcessorService.cs`, `HubContextSignalRPublisher.cs`, `HyperliquidAssetMetadataCache.cs`, `HyperliquidExecutionEngine.cs`, `HyperliquidOrderService.cs`, `MacroCalendarSyncWorker.cs`, `OptimizationProcessorService.cs`

8. **Add frontend feature modules**: `agents/`, `auth/`, `candle-management/`, `macro-calendar/`, `optimizer/`, `order-entry/`, `profile/`, `strategy-builder/`

9. **Add frontend core subfolders**: `guards/`, `interceptors/`, `pipes/`, `utils/`, `components/`

---

### Task 1.4: Update `10-architecture-decisions.md` {#task-14-update-architecture-decisions}

Fix incorrect ADRs and add new ones for decisions made during development.

- **Complexity**: Medium
- **Risk Factors**: Medium — ADRs must accurately reflect rationale
- **Files**:
  - `.agent-context/0-knowledge/10-architecture-decisions.md` — update
- **Success**:
  - ADR 7 reflects implemented JWT + Google SSO auth (not "deferred")
  - ADR 8 reflects Option C key management (not "encrypted at rest in DB")
  - ADR 13 reflects real ClaimsPrincipal-based identity (not "hardcoded dev stub")
  - 7 new ADRs added for decisions made during development

#### Changes Required

1. **Fix ADR 7 (Authentication)**: Replace "deferred until a later phase" and Azure AD B2C/Auth0 mentions. Actual: Custom JWT authentication with access + refresh tokens, email/password registration, Google OAuth via Google Identity Services. Components: `AuthController`, `JwtTokenService`, `GoogleTokenValidator`, `AspNetPasswordHasher`. Config: `JwtOptions`, `GoogleAuthOptions`.

2. **Fix ADR 8 (Subscriber Key Storage)**: Replace "encrypted at rest in the database" with Azure Key Vault. Actual: Under Option C, private keys never touch the server. `UserWalletAddress` stores only the wallet address. The private key is configured on the execution agent (Worker) via environment variable or config. `MutableSignerProvider` manages runtime key state.

3. **Fix ADR 13 (Identity Stub)**: Replace "hardcoded dev AppIdentity". Actual: `IdentityService` reads from `ClaimsPrincipal` in `HttpContext`, resolving `UserId` and `Email` from JWT claims. Fallback to `dev-user` only for unauthenticated calls (blocked by `[Authorize]` in production).

4. **Add ADR 17: Business Model Option C** — Worker as client-side execution agent; API as control plane; heartbeat protocol; reasons for choosing this over Options A and B.

5. **Add ADR 18: Per-User Network Routing** — `INetworkProvider`/`UserNetworkProvider` resolves mainnet vs testnet per-request from `User.PreferredNetwork`; `NetworkRoutingHandler` overrides HttpClient base URL.

6. **Add ADR 19: Azure SignalR as Production Backplane** — `AzureSignalRPublisher` uses `Microsoft.Azure.SignalR.Management`; chosen over Redis for simplicity and managed service benefits.

7. **Add ADR 20: Three Independent LLM Clients** — `ILlmClient` (strategy interpretation), `IReviewLlmClient` (review), `ILlmContextClient` (market context) — each with independent config sections and temperature settings.

8. **Add ADR 21: Macro Calendar as Trade Gate** — `MacroEvent` blocks trading during configurable pre/post windows; `MacroCalendarOptions` drives sync intervals.

9. **Add ADR 22: Strategy Optimization System** — Evolutionary sweep runner with fitness scoring, OOS validation; `OptimizationRun`/`OptimizationResult` persisted.

10. **Add ADR 23: Indicators as Separate Project** — `TradingApp.Indicators` is a standalone library with no external NuGet dependencies; Application depends on it.

## Phase Success Criteria

- All four foundation knowledge files accurately reflect the current codebase
- No aspirational content is presented as implemented
- New ADRs capture all major architectural decisions made during development
- Future Recommendations sections added to each file
