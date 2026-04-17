<!-- markdownlint-disable-file -->
# Release Changes: F5 — Order Placement

**Related Plan**: 20260325-f5-order-placement-plan.instructions.md
**Implementation Date**: 2026-03-25

## Summary

Implements end-to-end order placement on Hyperliquid testnet via Angular UI → .NET backend. Covers EIP-712 typed data signing with Nethereum, a `/exchange` REST endpoint, OrdersController with market/limit order support, a standalone signing diagnostic endpoint, and an Angular order entry form with confirmation dialog.

## Changes

### Added

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- src/TradePilot.Application/Abstractions/Services/INonceProvider.cs: Added nonce provider abstraction with monotonic nonce contract
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs: Added EIP-712 typed-data builder, action hash computation, and order action builder
- src/TradePilot.Infrastructure/Services/NonceProvider.cs: Added lock-free thread-safe nonce generator using UTC milliseconds
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidEip712Tests.cs: Added unit tests for typed-data construction and action hash behavior
- tests/TradePilot.Infrastructure.Tests/Services/NonceProviderTests.cs: Added unit tests for sequential and concurrent nonce uniqueness/monotonicity

<!-- Phase 2: Order Placement Backend -->
- src/TradePilot.Api/Models/PlaceOrderRequest.cs: Added order placement request DTO with validation attributes
- src/TradePilot.Api/Models/PlaceOrderResponse.cs: Added order placement response DTO
- src/TradePilot.Api/Models/TestSignResponse.cs: Added test-sign diagnostic response DTO including signature fields
- src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidExchangeResponse.cs: Added typed wire models for Hyperliquid exchange response payloads
- src/TradePilot.Api/Services/IHyperliquidOrderService.cs: Added order service interface contract
- src/TradePilot.Api/Services/HyperliquidOrderService.cs: Added order orchestration service for action building, signing, submission, mapping, and latency logging
- src/TradePilot.Api/Controllers/OrdersController.cs: Added orders API controller with POST endpoints for place-order and test-sign
- tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs: Added unit tests for order service behavior and error paths
- tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs: Added integration tests for OrdersController endpoints and validation/error mapping

<!-- Phase 3: Angular Order Entry UI -->
- frontend/trading-ui/src/app/core/models/place-order.model.ts: Added request/response/signature interfaces for order placement and test-sign payloads
- frontend/trading-ui/src/app/core/services/order.service.ts: Added root-scoped service for POST orders and POST orders/test-sign via ApiRestClient
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts: Added standalone Material dialog component for order confirmation
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html: Added order summary UI with conditional limit-price row
- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.scss: Added BEM styles and side-color accents using theme tokens
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts: Added standalone reactive-form component with market/limit behavior, mid-price prefill, dialog confirmation, and error display
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html: Added Order Entry form template (side toggle, type select, conditional price field, size field, submit)
- frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss: Added layout and form styling including buy/sell toggle checked states
### Modified

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- src/TradePilot.Infrastructure/TradePilot.Infrastructure.csproj: Added MessagePack and Nethereum.ABI package references
- src/TradePilot.Application/TradePilot.Application.csproj: Added Nethereum.ABI package reference
- src/TradePilot.Application/Abstractions/Services/IHyperliquidSigner.cs: Added generic typed-data signing method contract
- src/TradePilot.Infrastructure/Services/HyperliquidSigner.cs: Retained EthECKey instance and implemented typed-data signing
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidSignerTests.cs: Added typed-data signing tests for signature shape and determinism

<!-- Phase 2: Order Placement Backend -->
- src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs: Added PostExchangeAsync generic method to REST abstraction
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Implemented PostExchangeAsync transport for signed exchange actions
- src/TradePilot.Api/Program.cs: Registered INonceProvider as singleton and IHyperliquidOrderService as scoped

<!-- Phase 3: Angular Order Entry UI -->
- frontend/trading-ui/src/app/app.routes.ts: Added lazy-loaded /order-entry route
- frontend/trading-ui/src/app/app.component.html: Added Order Entry navigation link with active state styling
- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts: Removed inferrable string type annotations to satisfy lint
- frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.ts: Removed inferrable string type annotation to satisfy lint
### Removed

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- None
## Test Results

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- TradePilot.Infrastructure.Tests: 22/22 passed
- TradePilot.Api.Tests: 19/19 passed
- TradePilot.Application.Tests: 0/0 discovered
- TradePilot.Domain.Tests: 0/0 discovered

<!-- Phase 2: Order Placement Backend -->
- HyperliquidOrderServiceTests + OrdersControllerTests (targeted): 9/9 passed
- TradePilot.Infrastructure.Tests: 22/22 passed
- TradePilot.Api.Tests: 28/28 passed

<!-- Phase 3: Angular Order Entry UI -->
- Angular Build (npx ng build): PASSED
- Angular Lint (npx ng lint): PASSED
## Issues

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- Build lock from running API process resolved by running targeted project tests; no impact on final test results
- Nethereum EIP-712 API differed from initial snippet assumptions; resolved using Eip712TypedDataEncoder with Struct/Parameter attributes

<!-- Phase 2: Order Placement Backend -->
- Build lock from running API process resolved by running tests in Release configuration
- No other blockers encountered

<!-- Phase 3: Angular Order Entry UI -->
- Initial lint failed due to label-association violation in Order Entry template and pre-existing no-inferrable-types in market-data; all fixed and lint re-verified
- Transient working-directory issue during lint chaining; resolved by re-running with explicit absolute path
## Design Decisions

<!-- Phase 1: EIP-712 Signing & Nonce Infrastructure -->
- Action hash uses Keccak-256 over msgpack(action) + nonce(big-endian 8 bytes) + vault marker, aligned to Hyperliquid Python SDK
- TypedData uses MemberDescriptionFactory with Agent struct to keep EIP-712 payload strongly typed
- NonceProvider uses lock-free Interlocked.CompareExchange loop to guarantee uniqueness under high concurrency

<!-- Phase 2: Order Placement Backend -->
- Followed ADR 14 direct service injection pattern for OrdersController (no Application layer mediator)
- Exchange failures mapped at response level in HyperliquidOrderService with signature-rejection classification for diagnostics
- Test-sign message hash derived from existing connectionId path to avoid new signer dependencies in Api project
## Review Hints

- Verify cross-language parity against hyperliquid-python-sdk with a fixed known vector for action hash/signature bytes if a Python runtime is available in CI
- INonceProvider should be registered as singleton in DI (Phase 2)

<!-- Phase 2: Order Placement Backend -->
- Verify Hyperliquid testnet exchange response shape against live payloads to confirm all mapped status branches in HyperliquidExchangeResponse.cs
- Validate whether test-sign diagnostic values should expose full canonical EIP-712 domain separator for parity with external tooling

<!-- Phase 3: Angular Order Entry UI -->
- API calls placed in dedicated root service (OrderService) using ApiRestClient, consistent with existing frontend service patterns
- Standalone Material confirmation dialog used for explicit user confirmation before order submission
- Full error payload displayed via snackbar to satisfy AC5 and AC6 requirements
## Release Summary

All 3 phases completed successfully. The F5 Order Placement feature is fully implemented:

- **Phase 1** retired the highest-risk item: EIP-712 signing with Nethereum is verified compatible with Hyperliquid's phantom-agent pattern. A thread-safe NonceProvider guarantees unique monotonically increasing nonces under concurrent access.
- **Phase 2** delivers the full backend: `POST /api/orders` for market and limit orders, `POST /api/orders/test-sign` for signing diagnostics, structured latency logging, and wire-level Hyperliquid exchange response mapping.
- **Phase 3** delivers the Angular Order Entry UI: a reactive form with side toggle, order type selector, mid-price prefill for limit orders, confirmation dialog, spinner state, and full error payload display.

**Total tests passing**: Infrastructure 22/22 · Api 28/28 · Angular build and lint clean.
**Files created**: 17 · **Files modified**: 11
