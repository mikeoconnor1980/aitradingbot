# POC Review

**Reviewer:** GPT-5.4  
**Date:** 2026-03-26  
**Scope:** Code review of the current POC across Angular UI and .NET API

---

## Overall Verdict

The POC is materially beyond a thin spike.

It already has:

- a coherent Angular application structure
- a usable API surface for account, market-data, order, and streaming scenarios
- a sensible Hyperliquid integration split across signing, REST, websocket, and metadata concerns
- passing backend tests in the API and Infrastructure layers
- a clean Angular production build

This is meaningful progress.

The main issue is that some POC shortcuts are now crossing into correctness debt and architectural debt.

The biggest gaps are:

- one concrete dashboard order-management bug
- misleading 24h market-data presentation
- continued reliance on a single global wallet identity instead of the documented tenant-scoped model
- debug endpoints and dependency hygiene that should not survive much longer
- weak coverage in Application, Domain, and frontend tests

---

## Primary Findings

### 1. Cancel All Orders has a real UI bug

The dashboard clears its in-memory orders list before reading the asset it wants to cancel.

Relevant code:

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts`

Current flow:

- the UI snapshots `previousOrders`
- it sets `this.orders = []`
- it then calls `cancelAllOrders(this.orders[0]?.asset ?? "BTC")`

That means the call falls back to `BTC` instead of using the asset the user was viewing.

Why this matters:

- it is a real functional bug, not just a POC rough edge
- on any non-BTC asset, the wrong cancellation request can be sent
- this is exactly the kind of optimistic-UI issue that becomes dangerous in trading systems

### 2. The live ticker is mislabeling session-local values as 24h values

The streaming service seeds price stats from REST once, then mutates them locally from the incoming trade stream.

Relevant code:

- `src/TradePilot.Api/Services/MarketDataStreamService.cs`
- `frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.ts`

Current behavior:

- high and low are seeded from the current mid price at startup
- volume is seeded once from REST
- subsequent trades increase volume cumulatively in-process
- the UI renders the results as `24h High`, `24h Low`, and `24h Volume`

Why this matters:

- those labels imply exchange-truth 24h metrics
- after restart, those numbers reset or drift from actual 24h exchange values
- this creates misleading market context in the UI

For a trading product, data labeling accuracy matters more than it would in a generic dashboard.

### 3. The API is still built around a single global wallet

The current API runtime creates one signer from one configured private key at startup and reuses it across account and order services.

Relevant code:

- `src/TradePilot.Api/Program.cs`
- `src/TradePilot.Api/Services/HyperliquidAccountService.cs`
- `src/TradePilot.Api/Services/HyperliquidOrderService.cs`

This is consistent with a personal POC, but it directly conflicts with the documented target model:

- tenant-scoped credentials
- per-user exchange access
- per-user strategy execution

Why this matters:

- the more features added on top of this shape, the harder the multi-tenant migration becomes
- identity, service boundaries, controller contracts, and worker execution paths will all have to change later
- this is the most important architectural shortcut still present in the POC

### 4. Debug endpoints are still sitting on the main API surface

The orders controller exposes raw debug endpoints for:

- mids
- meta
- clearinghouse state

Relevant code:

- `src/TradePilot.Api/Controllers/OrdersController.cs`

Why this matters:

- they are useful during exchange integration work
- but they should not remain part of the main public surface once the POC moves beyond local development
- with the current stub identity model, they are especially easy to leave behind accidentally

### 5. Dependency hygiene needs attention now, not later

Running the backend tests surfaced a known high-severity advisory on AutoMapper 12.0.1.

Relevant files:

- `src/TradePilot.Application/TradePilot.Application.csproj`
- `src/TradePilot.Api/TradePilot.Api.csproj`

Why this matters:

- for a private POC, this is tolerable only for a short period
- for any shared preview or alpha, it should be fixed
- this is an easy piece of debt to remove before the project footprint grows further

### 6. Test coverage is uneven in a way that hides risk

Observed state:

- API tests are present and pass
- Infrastructure tests are present and pass
- Application and Domain test projects exist but currently contain no actual test cases
- frontend test coverage is minimal

Relevant paths:

- `tests/TradePilot.Api.Tests/`
- `tests/TradePilot.Infrastructure.Tests/`
- `tests/TradePilot.Application.Tests/`
- `tests/TradePilot.Domain.Tests/`
- `frontend/trading-ui/src/app/app.component.spec.ts`
- `frontend/trading-ui/src/app/features/connection/status-card.component.spec.ts`

Additional concern:

- `app.component.spec.ts` still mocks `HealthService`, while the component now depends on `SignalRService`

Why this matters:

- this indicates the UI tests are not tracking actual runtime behavior closely
- the Application and Domain layers currently have no automated behavioral safety net
- in a trading system, correctness pressure belongs in those layers, not only in controller-level tests

---

## What Looks Strong

### 1. The POC already has useful product shape

This is not just a backend integration sandbox.

The Angular app is already broken into distinct flows for:

- dashboard/account state
- market data
- order entry
- connection status

That is enough product structure to start learning from actual usage.

### 2. The Hyperliquid integration boundaries are mostly sensible

The split across:

- REST client
- websocket client
- signer
- asset metadata cache
- API-facing services

is directionally good.

That gives you a path to refactor the identity model later without rewriting everything from scratch.

### 3. SignalR streaming is a good POC choice

For a POC, the architecture of:

- shared websocket market stream
- API-hosted background service
- SignalR relay to Angular

is pragmatic and appropriate.

It is not the final production shape, but it is a good way to prove the user-facing real-time loop quickly.

### 4. API and Infrastructure test coverage are better than average for a POC

Passing backend tests in those layers is a positive signal.

It means the project is already doing more than manual clicking and optimistic assumptions.

---

## Validation Notes

### Build and test signals

Validated during review:

- Angular production build completed successfully
- API tests passed
- Infrastructure tests passed
- Application and Domain test projects discovered zero tests

### Runtime note

During the earlier part of the review, the expected localhost ports were not serving when checked.

That prevented a fully interactive browser pass at that moment.

The code review findings above are therefore based on:

- source inspection
- templates and service wiring
- test output
- build output

rather than a full interactive product walkthrough.

---

## Bottom Line

The POC is in good shape for its stage.

The project now looks like a real product in progress, not just a concept.

The next step is not adding more surface area as quickly as possible. The next step is hardening the parts that are already there:

1. fix the concrete order-management bug
2. correct the market-data semantics or relabel the UI honestly
3. start introducing the real tenant and credential boundaries
4. remove or isolate debug-only API surface
5. bring Application, Domain, and frontend tests up to the same standard as API and Infrastructure

If those are addressed soon, the POC will be a strong base for the next phase.
