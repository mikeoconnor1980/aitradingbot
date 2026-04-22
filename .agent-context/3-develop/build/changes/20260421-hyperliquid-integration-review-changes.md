<!-- markdownlint-disable-file -->
# Release Changes: Hyperliquid Integration Code Review Remediation

**Related Plan**: 20260421-hyperliquid-integration-review-plan.instructions.md
**Implementation Date**: 2026-04-21

## Summary

Completed implementation for all four Hyperliquid integration review-remediation phases across shared utilities, WebSocket hardening, order execution, and asset/timeframe support, with repeated solution builds and focused Hyperliquid verification throughout the run.

## Changes

### Added

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidFormatting.cs: Added the shared Hyperliquid wire-format, side-mapping, and decimal-parsing utility used across infrastructure and API code.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidFormattingTests.cs: Added focused regression coverage for wire decimal formatting, side mapping, and decimal parsing edge cases.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidHistoricalDataClientTests.cs: Added tests for candle forwarding, direct exception propagation, and empty funding-rate history behavior.

### Modified

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- src/TradePilot.Application/Abstractions/Services/ExchangeCapabilitySet.cs: Added SupportsFundingRateHistory to the exchange capability contract.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidHistoricalDataClient.cs: Replaced ContinueWith with async/await and returned an empty funding-rate history result instead of throwing.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs: Declared Hyperliquid funding-rate history support explicitly as false.
- src/TradePilot.Infrastructure/Binance/BinanceCapabilities.cs: Declared Binance funding-rate history support explicitly as true.
- src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs: Removed nullable signer usage and switched Hyperliquid side and decimal parsing to the shared formatting utility.
- src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs: Removed duplicated Hyperliquid parsing and side-mapping helpers in favor of HyperliquidFormatting.
- src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs: Removed the duplicated side-mapping helper and imported the shared Hyperliquid formatting utility.

<!-- Phase 2: WebSocket Client Hardening -->
- src/TradePilot.Infrastructure/Services/HyperliquidWebSocketClient.cs: Added linked connect timeout handling, increased the receive buffer, and hardened ping-loop shutdown.
- src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs: Added linked connect timeout handling, increased the receive buffer, and aligned ping-loop cleanup with explicit cancel-and-await behavior.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidWebSocketClientTests.cs: Added regression tests for connect timeout and receive buffer settings.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidUserEventClientTests.cs: Added regression tests for connect timeout and receive buffer settings.

<!-- Phase 3: Order Execution Improvements -->
- src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs: Added configurable market-order slippage in basis points with a backward-compatible default.
- src/TradePilot.Api/Models/PlaceOrderResponse.cs: Added a Warnings collection for additive non-fatal order issues.
- src/TradePilot.Api/Services/HyperliquidOrderService.cs: Injected Hyperliquid options, applied configurable market slippage, switched to shared wire formatting, rewrote significant-figure rounding with decimal math, and surfaced companion trigger failures as warnings.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs: Switched order and trigger payload formatting to HyperliquidFormatting.ToWireDecimal.
- src/TradePilot.Infrastructure/Services/MutableSignerProvider.cs: Reduced wallet-address logging verbosity to Debug while keeping a non-address information log.
- src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs: Removed the final duplicated ToWireDecimal helper and switched to shared formatting.
- tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs: Updated constructor setup and added regression coverage for configurable slippage, warning propagation, and decimal rounding.

<!-- Phase 4: Asset & Timeframe Expansion -->
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs: Relaxed coin validation, expanded supported timeframes, documented quick-pick coins, and added explicit timeframe normalization.
- src/TradePilot.Infrastructure/Hyperliquid/HyperliquidCapabilities.cs: Preserved all supported Hyperliquid timeframes in the capability set without collapsing `1m` and `1M`.
- src/TradePilot.Api/Controllers/CandlesController.cs: Replaced stale hardcoded Hyperliquid validation messages with format-based symbol guidance and dynamic timeframe output.
- tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs: Added coverage for relaxed coin validation, HIP-3 symbols, invalid formats, and all supported timeframes.
- tests/TradePilot.Infrastructure.Tests/Services/ExchangeAbstractionAdaptersTests.cs: Added coverage for expanded Hyperliquid capabilities and non-hardcoded symbol mapping eligibility.

### Removed

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- None

<!-- Phase 2: WebSocket Client Hardening -->
- None

<!-- Phase 3: Order Execution Improvements -->
- None

<!-- Phase 4: Asset & Timeframe Expansion -->
- None

## Test Results

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- Targeted infrastructure test scope: 23/23 passed via TradePilot.Infrastructure.Tests filtered to HyperliquidFormattingTests and HyperliquidHistoricalDataClientTests.
- Solution build: PASSED.
- Full solution test suite: 1299/1299 passed via `dotnet test TradePilot.sln --no-build`.
- TradePilot.Indicators.Tests: 59/59 passed.
- TradePilot.Domain.Tests: 97/97 passed.
- TradePilot.AI.Tests: 42/42 passed.
- TradePilot.Infrastructure.Tests: 128/128 passed.
- TradePilot.Persistence.Tests: 36/36 passed.
- TradePilot.Application.Tests: 629/629 passed.
- TradePilot.Worker.Tests: 58/58 passed.
- TradePilot.Api.Tests: 250/250 passed.

<!-- Phase 2: WebSocket Client Hardening -->
- Focused WebSocket infrastructure tests: 24/24 passed across HyperliquidWebSocketClientTests and HyperliquidUserEventClientTests.
- Solution build: PASSED.
- Full solution test suite: 1299/1299 passed via `dotnet test .\TradePilot.sln --no-build --logger "console;verbosity=minimal"`.
- TradePilot.Infrastructure.Tests: 132/132 passed.
- TradePilot.Api.Tests: 250/250 passed.

<!-- Phase 3: Order Execution Improvements -->
- HyperliquidOrderService focused tests: 21/21 passed.
- LiveExecutionEngine and MutableSignerProvider focused infrastructure tests: 27/27 passed.
- Solution build: PASSED.
- TradePilot.Api.Tests project: 255/255 passed.
- Full solution test suite: 1308/1308 passed via `dotnet test .\TradePilot.sln --no-build --logger "console;verbosity=minimal"`.

<!-- Phase 4: Asset & Timeframe Expansion -->
- Solution build: PASSED.
- TradePilot.Infrastructure.Tests AssetMapper scope: 66/66 passed.
- TradePilot.Infrastructure.Tests Hyperliquid scope: 100/100 passed.
- TradePilot.Api.Tests Hyperliquid scope: 42/42 passed.

## Issues

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- The dedicated test runner did not resolve the individual new test-file paths, so targeted validation used `dotnet test` against the infrastructure test project with a class-name filter.
- The first targeted test run failed because HyperliquidUserEventClient was missing a using for the new HyperliquidFormatting namespace; adding that import resolved the failure.

<!-- Phase 2: WebSocket Client Hardening -->
- The full solution test command exceeded the foreground timeout and continued in a background terminal; the final terminal output confirmed 1299/1299 passing, and the redundant rerun terminal was terminated.

<!-- Phase 3: Order Execution Improvements -->
- HyperliquidOrderServiceTests initially failed to build after the constructor gained `IOptions<HyperliquidOptions>`; updating the test setup to inject options resolved the issue.
- The new rounding tests initially failed to compile because `CultureInfo` was missing; adding the missing using resolved the failure.
- The full solution test run exceeded the foreground terminal timeout; the final 1308/1308 pass result was confirmed from terminal output after separate no-build validation of TradePilot.Api.Tests.

<!-- Phase 4: Asset & Timeframe Expansion -->
- `1m` and `1M` initially collided in case-insensitive timeframe collections, which dropped monthly support from the mapper and capabilities. Explicit timeframe normalization in the mapper and an ordinal timeframe set in capabilities resolved the collision.
- A broad multi-file test-tool invocation reported ambiguous project build failures without exposing the actual defect. Direct project-level `dotnet test` commands matching the phase verification requirements were used instead.

## Design Decisions

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- Hyperliquid funding-rate history now advertises support through SupportsFundingRateHistory and returns an empty result instead of throwing, preserving substitutability for the historical data client abstraction.
- Hyperliquid decimal parsing was standardized to return `0m` for null, empty, or malformed values, matching the safer account-service behavior and avoiding hard failures on optional exchange fields.
- Binance capabilities now set SupportsFundingRateHistory explicitly to keep support semantics unambiguous across exchanges.

<!-- Phase 2: WebSocket Client Hardening -->
- `ConnectTimeout` and `ReceiveBufferSize` were made `internal` so the infrastructure tests can assert the hardening settings directly without adding a larger WebSocket abstraction.
- Both receive loops now explicitly cancel and await their ping tasks in `finally`, since disposing a linked cancellation source alone does not stop a background ping loop.

<!-- Phase 3: Order Execution Improvements -->
- Kept market-order slippage backward-compatible by defaulting `MarketOrderSlippageBps` to `500`.
- Preserved `Detail` for primary order-failure semantics and used additive `Warnings` only for companion trigger-order failures.
- Removed the remaining `ToWireDecimal` duplication in LiveExecutionEngine so the shared formatter is now the single implementation.

<!-- Phase 4: Asset & Timeframe Expansion -->
- Kept `GetSupportedCoins()` as a documented quick-pick subset rather than removing it, because the phase details allowed retaining it as a convenience method while clarifying that it is not the full live asset universe.
- Used format-based coin validation instead of metadata-backed validation in HyperliquidAssetMapper, because live asset verification belongs to the metadata cache and the mapper should not block newer Hyperliquid or HIP-3 assets.
- Updated the API candle validation messages to avoid stale hardcoded support lists after expanding mapper behavior.

## Review Hints

<!-- Phase 1: Shared Utilities & Critical Code Fixes -->
- Review exchange-capability consumers to decide whether any call sites should start checking SupportsFundingRateHistory before requesting historical funding data.
- Review the malformed-decimal behavior change in the Hyperliquid REST and account services, since invalid values now degrade to `0m` instead of throwing.

<!-- Phase 2: WebSocket Client Hardening -->
- Review the explicit ping-loop shutdown path in both WebSocket clients, especially around remote close and reconnect scenarios, since cleanup now depends on the new `finally`-based cancel-and-await flow.
- Review whether the 15-second connect timeout is appropriate for slower or transiently degraded environments.

<!-- Phase 3: Order Execution Improvements -->
- Review any API or UI consumer that displays PlaceOrderResponse so `Warnings` are surfaced distinctly from hard failures.
- Review worker-side order payload formatting changes in LiveExecutionEngine for parity expectations with API-side order submission.

<!-- Phase 4: Asset & Timeframe Expansion -->
- Review any consumer assumptions around timeframe case sensitivity, especially if anything outside the mapper starts reading capability-set timeframes directly rather than going through normalized mapper methods.
- Review any UI flow still treating `GetSupportedCoins()` as the full Hyperliquid market universe; it remains only a convenience subset and not authoritative live metadata.

## Release Summary

Implemented all 4 phases and completed all 26 planned tasks. The Hyperliquid integration now centralizes wire-format and parsing helpers, removes the historical-data `ContinueWith` and funding-rate substitutability issues, hardens both WebSocket clients with timeout and ping-loop cleanup behavior, makes market-order slippage configurable, replaces floating-point rounding with decimal-only logic, surfaces companion trigger-order failures through response warnings, relaxes asset validation for new and HIP-3 symbols, and expands timeframe support to Hyperliquid's full 13 supported intervals. Validation included repeated solution builds, focused Hyperliquid test runs, and full solution test passes up to 1308/1308 during the implementation sequence.
