<!-- markdownlint-disable-file -->
# Release Changes: Binance Integration Fixes

**Related Plan**: 20260422-binance-integration-fixes-plan.instructions.md
**Implementation Date**: 2026-04-22

## Summary

Implementing the Binance integration safety, correctness, resilience, and architecture fixes identified in the 2026-04-21 tribunal review.

## Changes

### Added

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- src/TradePilot.Application/Abstractions/Exceptions/ExchangeApiException.cs: Added an exchange-agnostic base exception carrying exchange status code and error category.
- src/TradePilot.Infrastructure/Binance/BinanceParsing.cs: Added shared Binance parsing helpers for decimals, integers, and order IDs.
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceParsingTests.cs: Added unit coverage for parsing behavior and funding-rate scientific notation.
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceExchangeInfoCacheTests.cs: Added cache freshness, expiry, and semaphore-cancellation tests.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs: Added focused unit coverage for normalization, cancel behavior, modify compensation, and leverage margin ordering.

<!-- Phase 3: Account & Market Data Consistency -->
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceAccountAdapterTests.cs: Added focused coverage for supported-asset consistency, account-summary parsing, expanded asset positions, and sequential fills fetching.
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceMarketMetadataProviderTests.cs: Added coverage for successful open-interest population and warning-backed zero fallback on open-interest failures.

<!-- Phase 4: Resilience & Error Handling -->
- src/TradePilot.Application/Abstractions/Exceptions/BinanceApiException.cs: Added a Binance-specific exchange exception carrying Binance error codes and transient or permanent classification.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- src/TradePilot.Application/Abstractions/Services/IExchangeSymbolMetadataProvider.cs: Added the exchange-agnostic symbol metadata contract and shared metadata record.
- src/TradePilot.Infrastructure/Binance/BinanceSymbolMetadataProvider.cs: Added the Binance adapter that maps `IBinanceExchangeInfoCache` data to the new abstraction.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidSymbolMetadataProvider.cs: Added the Hyperliquid adapter backed by shared REST metadata.
- tests/TradePilot.Infrastructure.Tests/Binance/BinanceSymbolMetadataProviderTests.cs: Added focused unit coverage for Binance symbol metadata mapping.
- tests/TradePilot.Infrastructure.Tests/Hyperliquid/HyperliquidSymbolMetadataProviderTests.cs: Added focused unit coverage for Hyperliquid symbol metadata mapping.

### Modified

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- src/TradePilot.Application/Abstractions/Exceptions/HyperliquidApiException.cs: Moved Hyperliquid API exceptions onto the new exchange-agnostic base type.
- src/TradePilot.Application/Abstractions/Exceptions/RateLimitException.cs: Decoupled rate-limit handling from Hyperliquid-specific inheritance and constructor shape.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Updated rate-limit exception construction to pass exchange status code.
- src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs: Updated rate-limit exception construction to pass exchange status code.
- src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs: Updated rate-limit exception construction to pass exchange status code.
- src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs: Replaced duplicated decimal parsing and raw order ID parsing with shared BinanceParsing helpers.
- src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs: Replaced silent-zero parsing with explicit shared parsing and fallback handling.
- src/TradePilot.Infrastructure/Binance/BinanceMarketMetadataProvider.cs: Replaced local parsing helpers with shared parsing and added FormatException handling.
- src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs: Switched cache freshness to monotonic Stopwatch timing and added IDisposable.
- src/TradePilot.Infrastructure/Binance/Models/BinanceFundingRate.cs: Changed funding-rate parsing to NumberStyles.Any for scientific notation.
- tests/TradePilot.Api.Tests/Services/BinanceFuturesRestClientTests.cs: Strengthened the rate-limit assertion to verify the new exchange status code contract.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- src/TradePilot.Application/Abstractions/Services/IBinanceFuturesAuthClient.cs: Added the authenticated Binance margin-type contract.
- src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs: Implemented `SetMarginTypeAsync` with signed query handling and Binance `-4046` idempotency handling.
- src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs: Added metadata-driven normalization, fail-fast cancel behavior, modify compensation retry logic, and margin-type switching before leverage updates.
- tests/TradePilot.Api.Tests/Services/BinanceFuturesAuthClientTests.cs: Added coverage for the Binance `-4046` margin-type-already-set success path.
- tests/TradePilot.Worker.Tests/Services/BinanceSessionHostIntegrationTests.cs: Updated the fake Binance public exchange-info response so the worker integration path resolves new normalization metadata.

<!-- Phase 3: Account & Market Data Consistency -->
- src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs: Added the authoritative SupportedAssets set derived from the mapped Binance symbols.
- src/TradePilot.Infrastructure/Binance/BinanceCapabilities.cs: Switched supported-asset reporting to delegate to BinanceAssetMapper.
- src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs: Replaced local parsing with shared BinanceParsing helpers, expanded supported-asset filtering to all mapped assets, and changed recent fills fan-out to sequential requests.
- src/TradePilot.Infrastructure/Binance/BinanceMarketMetadataProvider.cs: Replaced local parsing helpers, added real `/fapi/v1/openInterest` fetching, and kept open interest best-effort with warning logging and zero fallback.

<!-- Phase 4: Resilience & Error Handling -->
- src/TradePilot.Api/Program.cs: Added retry plus per-attempt timeout resilience to the named `binance-public` client.
- src/TradePilot.Worker/Program.cs: Added the same `binance-public` resilience pipeline as the API host.
- src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs: Expanded Binance error mapping to cover 403, 418, 451, 5xx, and business error codes `-1111`, `-2019`, and `-4003`.
- src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs: Generalized exchange API exception handling so Binance exchange exceptions map consistently to HTTP responses.
- tests/TradePilot.Api.Tests/Services/BinanceFuturesAuthClientTests.cs: Added focused coverage for permanent and transient Binance exceptions plus the new business error mappings.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- src/TradePilot.Api/Controllers/OrdersController.cs: Removed exchange-specific asset metadata branching and switched asset listing to keyed `IExchangeSymbolMetadataProvider` resolution while preserving canonical `*-PERP` API output.
- src/TradePilot.Api/Program.cs: Registered keyed `IExchangeSymbolMetadataProvider` services for Hyperliquid and Binance.
- src/TradePilot.Worker/Program.cs: Registered keyed `IExchangeSymbolMetadataProvider` services for Hyperliquid and Binance in the worker host.
- tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs: Added asset-list endpoint coverage for both exchanges and updated test doubles to satisfy current subscription guards.

### Removed

## Test Results

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- Focused Binance REST client tests in TradePilot.Api.Tests: 6/6 passed.
- Focused Binance infrastructure tests in TradePilot.Infrastructure.Tests: 44/44 passed.
- TradePilot.Domain.Tests: 97/97 passed.
- TradePilot.Application.Tests: 629/629 passed.
- TradePilot.AI.Tests: 42/42 passed.
- TradePilot.Indicators.Tests: 59/59 passed.
- TradePilot.Infrastructure.Tests: 189/189 passed.
- TradePilot.Persistence.Tests: 36/36 passed.
- TradePilot.Worker.Tests: 58/58 passed.
- TradePilot.Api.Tests: 254/256 passed.
- Solution build: `dotnet build TradePilot.sln --no-restore` passed.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- BinanceExecutionEngineTests: 9/9 passed.
- BinanceFuturesAuthClientTests: 3/3 passed.
- TradePilot.Infrastructure.Tests: 183/183 passed.
- TradePilot.Worker.Tests: 58/58 passed.
- TradePilot.Api.Tests: 253/255 passed.
- Solution build: PASSED.
- CandlesControllerTests disambiguation run: 22/26 passed.

<!-- Phase 3: Account & Market Data Consistency -->
- Focused BinanceAccountAdapterTests + BinanceMarketMetadataProviderTests: 6/6 passed.
- TradePilot.Infrastructure.Tests: 189/189 passed.
- TradePilot.Worker.Tests: 58/58 passed.
- TradePilot.Domain.Tests: 97/97 passed.
- TradePilot.Application.Tests: 629/629 passed.
- TradePilot.Persistence.Tests: 36/36 passed.
- TradePilot.AI.Tests: 42/42 passed.
- TradePilot.Indicators.Tests: 59/59 passed.
- TradePilot.Api.Tests: 254/256 passed.
- Solution build: PASSED.

<!-- Phase 4: Resilience & Error Handling -->
- BinanceFuturesAuthClientTests: 8/8 passed.
- Full solution build: PASSED.
- Full solution test suite: 1369/1371 passed.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- OrdersControllerTests: 18/18 passed.
- BinanceSymbolMetadataProviderTests: 2/2 passed.
- HyperliquidSymbolMetadataProviderTests: 2/2 passed.
- Full solution build: PASSED.
- Full solution tests: 1375/1377 passed.

## Issues

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- A stale `testhost` process locked TradePilot.Api.Tests output assemblies and caused MSB3026/MSB3027/MSB3021 copy failures; this was resolved by stopping the process and rerunning build-then-test.
- Two unrelated API controller tests remain failing in tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs because they assert Binance validation semantics on the Hyperliquid ingest route implemented in src/TradePilot.Api/Controllers/CandlesController.cs.
- A rerun of `TradePilot.Api.Tests` required a longer timeout window but finished with the same two unrelated `CandlesControllerTests` failures, confirming the Phase 1 slice is otherwise green.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- The worker integration test initially failed because its fake `binance-public` client returned `{}` for `/fapi/v1/exchangeInfo`; this was fixed by returning minimal BTC exchange-info metadata in the test stub.
- The full API test project still has 2 unrelated pre-existing failures in `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs`.
- A background rerun confirmed the same 2 `CandlesControllerTests` failures after the full API test project exceeded the shorter foreground timeout; no Binance execution-engine regressions were present.

<!-- Phase 3: Account & Market Data Consistency -->
- The dedicated `runTests` tool initially reported the focused Binance infrastructure slice as a project build failure, but direct `dotnet test` execution for the same tests passed cleanly.
- `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` still has 2 unrelated failures outside the Binance account and market metadata slice.
- The API test project took over 220 seconds to complete in this environment because host startup seeded background services before returning the final test summary.

<!-- Phase 4: Resilience & Error Handling -->
- The first focused validation failed because the new tests were missing the TradePilot.Application.Abstractions.Services namespace import and used the wrong BinancePlaceOrderRequest construction pattern; both issues were fixed before rerunning the phase tests.
- The full solution test run still reports the same 2 unrelated pre-existing failures in `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs`.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- The first focused `OrdersControllerTests` run failed because the new tests were missing `System.Text.Json`; that import was added before rerunning the focused scope successfully.
- Existing order and trigger controller tests initially failed because the mocked `ISubscriptionFeatureService` did not model the controller's current subscription guard behavior; the test setup was updated to return an active policy and asset-allowance behavior.
- The full solution test suite still has the same 2 unrelated pre-existing failures in `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs`.

## Design Decisions

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- Left existing `catch (HyperliquidApiException)` sites Hyperliquid-specific because the current handlers are for signature-rejection flows rather than generic exchange rate limiting.
- Used `BinanceParsing.TryParseDecimal` only where the existing code already had explicit fallback sources for optional balance fields; mandatory numeric fields now fail fast to avoid silent corruption.
- Kept the existing Binance max leverage lookup table unchanged in this phase because the required hardening work was cache timing and disposal, not asset-metadata broadening.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- Kept `ModifyTriggerOrderAsync` as `Task` instead of widening the `IExecutionEngine` contract; recovery retries the replacement order once and throws a detailed `DomainException` if both attempts fail.
- Implemented Binance margin-type switching through the existing signed query-string request pattern so it remains compatible with the current Binance signing handler.
- Added fail-fast checks for limit and trigger prices that normalize to zero, alongside the zero-size guard, to avoid sending obviously invalid exchange requests.

<!-- Phase 3: Account & Market Data Consistency -->
- Used BinanceAssetMapper as the single source of truth for supported Binance assets so capabilities and account reads cannot drift from the symbol-mapping table.
- Kept open-interest retrieval best-effort so market metadata remains available when the Binance OI endpoint is transiently unavailable.
- Kept recent fills retrieval sequential and ordered by normalized asset symbol to avoid request bursts after expanding supported assets beyond BTC and ETH.

<!-- Phase 4: Resilience & Error Handling -->
- Generalized the API exception filter to `ExchangeApiException` instead of adding Binance-only branching so future exchange-specific API exceptions stay consistent at the host boundary.
- Kept rate-limit retry timing sourced from the HTTP `Retry-After` header already available in `BinanceFuturesAuthClient` rather than adding redundant body parsing for retry metadata.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- Implemented `HyperliquidSymbolMetadataProvider` against `IHyperliquidRestClient` instead of the API-only Hyperliquid metadata cache so the abstraction remains usable in both API and Worker hosts.
- Derived Hyperliquid price decimals from the existing live execution normalization rule for perps so the shared metadata shape stays consistent with current order-price normalization behavior.
- Preserved the public `api/orders/assets` response shape by continuing to emit canonical `ASSET-PERP` symbols even though the new abstraction carries the native exchange symbol internally.

## Review Hints

<!-- Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening -->
- Review downstream callers that may later want to catch `ExchangeApiException` instead of only `HyperliquidApiException` or `RateLimitException` as subsequent Binance phases broaden error handling.
- Review the mismatch between tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs and src/TradePilot.Api/Controllers/CandlesController.cs before treating the remaining 2 API test failures as a Binance regression.

<!-- Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin -->
- Review callers of single-argument `CancelOrderAsync` for the new exception path after process restart or any other loss of in-memory order-to-asset mapping.
- Review whether a future phase should return the replacement order id from `ModifyTriggerOrderAsync`; the current interface preserved compatibility, but the new order id is only tracked internally.
- Review the existing `CandlesControllerTests` failures separately from this phase; they still assert behavior that does not match the current controller implementation.

<!-- Phase 3: Account & Market Data Consistency -->
- Review the expanded 8-asset account and fills fan-out for production rate-limit behavior, especially if additional supported assets are added later.
- Review `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` separately from this phase; those failures are the only blocker to a fully green verification run.

<!-- Phase 4: Resilience & Error Handling -->
- Review the new exchange-exception filter behavior in `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs`, since it now covers Binance exceptions in addition to Hyperliquid-derived ones.
- Review the resilience parity between `src/TradePilot.Api/Program.cs` and `src/TradePilot.Worker/Program.cs` so the `binance-public` named client stays identical across both hosts.

<!-- Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata -->
- Review `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidSymbolMetadataProvider.cs`, since it currently reads Hyperliquid metadata on demand via `IHyperliquidRestClient` rather than a shared host-neutral cache.
- Review `src/TradePilot.Api/Controllers/OrdersController.cs` to confirm preserving canonical `*-PERP` output remains the intended public API contract after introducing native-symbol metadata internally.
- Review `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` separately from this phase, since those 2 failures remain the only blocker to a fully green solution test run.

## Release Summary

Implemented all five planned Binance hardening phases across execution safety, account and market metadata consistency, HTTP resilience, and exchange-agnostic symbol metadata.

The implementation resolved the targeted CRITICAL and MAJOR review findings by adding fail-fast cancel behavior, metadata-driven size and price normalization, trigger-order recovery retries, margin-type switching, expanded Binance error mapping, resilient public HTTP clients, real open-interest retrieval, unified supported assets, and a shared exchange symbol metadata abstraction now used by OrdersController.

Validation completed with a clean solution build and focused plus broad test coverage for the touched Binance and controller slices. The only remaining red checks are 2 pre-existing unrelated failures in `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs`, which were reproduced throughout the implementation and are not caused by the Binance changes.
