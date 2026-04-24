<!-- markdownlint-disable-file -->

# Task Details: Binance Integration Review Round 2 Fixes

## Phase 3: Bounded-Parallel Fills Retrieval

## Standards and Knowledge References

- **C# Standards** (`.github/instructions/csharp.instructions.md`): Use `sealed` classes, PascalCase naming, underscore-prefixed private fields.
- **Testing Standards** (`.github/instructions/testing.instructions.md`): MSTest framework, Moq for mocking, FluentAssertions ≤ v6 for assertions. Given_When_Then naming.
- **Binance Integration** (`.agent-context/0-knowledge/23-binance-integration.md`): `BinanceAccountAdapter.GetRecentFillsAsync` currently fetches fills per-symbol sequentially. Each `userTrades` call costs 5 weight against the 2400 weight/min authenticated rate limit.

## Design References

- **Current pattern** (`BinanceAccountAdapter.GetRecentFillsAsync`): Uses `foreach` + `await` per symbol, accumulates into `List<BinanceUserTradeSnapshot>`, then sorts descending by time.
- **Existing test** (`BinanceAccountAdapterTests.GivenNoPairFilter_WhenGetRecentFillsAsync_ThenQueriesMappedSymbolsSequentially`): Tracks `maxConcurrency` via `Interlocked.Increment/Decrement` and asserts `maxConcurrency.Should().Be(1)`. This assertion must be updated.
- **Rate limit safety**: At 3 concurrent requests × 5 weight = 15 weight per batch. With 8 assets, that's 3 batches × 15 weight = 45 weight total — well within 2400/min.

### Task 3.1: Implement bounded-parallel fills fetching {#task-31-implement-bounded-parallel-fills}

Replace the sequential `foreach` loop in `GetRecentFillsAsync` with `Parallel.ForEachAsync` using `MaxDegreeOfParallelism = 3`. Use a thread-safe collection for accumulation.

- **Complexity**: Low
- **Risk Factors**: Must use thread-safe collection (`ConcurrentBag`) for accumulating trades from parallel tasks. Must preserve the final sort order (descending by time).
- **Files**:
  - `src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs` — Modify `GetRecentFillsAsync`
- **Success**:
  - Fills are fetched with up to 3 concurrent requests
  - Thread-safe accumulation with no data races
  - Results are still sorted descending by time
  - Single-symbol case (pair filter) still works correctly (no parallelism needed for 1 symbol)
- **Dependencies**:
  - None — independent of Phases 1 and 2

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs — modification
// Replace the sequential foreach loop in GetRecentFillsAsync:

// BEFORE:
//     List<BinanceUserTradeSnapshot> trades = [];
//
//     foreach (var symbol in symbols)
//     {
//         var symbolTrades = await _authClient.GetUserTradesAsync(symbol, cancellationToken: cancellationToken);
//         trades.AddRange(symbolTrades);
//     }

// AFTER:
    ConcurrentBag<BinanceUserTradeSnapshot> trades = [];

    await Parallel.ForEachAsync(
        symbols,
        new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
        async (symbol, ct) =>
        {
            var symbolTrades = await _authClient.GetUserTradesAsync(symbol, cancellationToken: ct);
            foreach (var trade in symbolTrades)
            {
                trades.Add(trade);
            }
        });

    return trades
        .OrderByDescending(trade => trade.Time)
        .Select(MapFill)
        .ToList();
```

**Note**: `ConcurrentBag<T>` is used instead of a shared `List<T>` to avoid data races during parallel accumulation. The final `.OrderByDescending()` ensures deterministic output order regardless of parallel execution timing.

##### Pattern References

- Existing `GetRecentFillsAsync` in `BinanceAccountAdapter.cs` (lines 97-113) — the method being modified.
- `Task.WhenAll` pattern used in `GetAccountSummaryAsync` and `GetPositionsAsync` (same class) — demonstrates the codebase's existing comfort with parallel async operations.

---

### Task 3.2: Update unit tests for parallel fills {#task-32-update-fills-tests}

Update the existing fills test to allow concurrent requests and verify that bounded parallelism works correctly.

- **Complexity**: Low
- **Risk Factors**: The existing test uses `Interlocked` to track concurrency — the tracking mechanism works for parallel execution, only the final assertion needs updating.
- **Files**:
  - `tests/TradePilot.Infrastructure.Tests/Binance/BinanceAccountAdapterTests.cs` — Update fills test
- **Success**:
  - Test renamed to reflect parallel behavior
  - `maxConcurrency` assertion updated from `.Be(1)` to `.BeInRange(1, 3)` (allows bounded parallelism)
  - All fills are still returned and sorted correctly
  - Single-symbol test (with pair filter) still passes
- **Dependencies**:
  - Task 3.1 (the production code change must be in place)

#### Implementation Details

```csharp
// tests/TradePilot.Infrastructure.Tests/Binance/BinanceAccountAdapterTests.cs — modification

// 1. Rename existing test:
// OLD: GivenNoPairFilter_WhenGetRecentFillsAsync_ThenQueriesMappedSymbolsSequentially
// NEW:
[TestMethod]
public async Task GivenNoPairFilter_WhenGetRecentFillsAsync_ThenQueriesMappedSymbolsWithBoundedParallelism()
{
    // ... existing arrange/act code stays the same ...

    // Only the final assertion for maxConcurrency changes:
    // BEFORE:
    // maxConcurrency.Should().Be(1);
    // AFTER:
    maxConcurrency.Should().BeInRange(1, 3);

    // All other assertions remain unchanged:
    result.Should().HaveCount(BinanceAssetMapper.SupportedAssets.Count);
    result.Select(fill => fill.Asset).Should().BeEquivalentTo(BinanceAssetMapper.SupportedAssets);
    requestedSymbols.Should().BeEquivalentTo(BinanceAssetMapper.SupportedAssets.Select(BinanceAssetMapper.ToFuturesSymbol));
    result.Select(fill => fill.Timestamp).Should().BeInDescendingOrder();
}
```

**Note**: Use `.BeInRange(1, 3)` rather than `.Be(3)` because `Parallel.ForEachAsync` may not always saturate the concurrency limit (e.g., in fast test environments with minimal delay, some requests may complete before all are started). The key assertion is that it's no longer restricted to 1.

##### Pattern References

- Existing test `GivenNoPairFilter_WhenGetRecentFillsAsync_ThenQueriesMappedSymbolsSequentially` in `BinanceAccountAdapterTests.cs` — the test being modified.

---

### Task 3.3: Build and verify all tests pass {#task-33-build-and-verify}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None expected — this is a low-risk change.
- **Files**:
  - Solution level — all projects
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test TradePilot.sln` — all tests pass
  - No new warnings in modified files
- **Dependencies**:
  - Tasks 3.1, 3.2

## Phase Success Criteria

- `GetRecentFillsAsync` fetches fills with up to 3 concurrent requests per batch
- Results are still sorted descending by time
- Thread-safe accumulation with no data races
- Test verifies bounded parallelism (no longer asserts sequential execution)
- All existing tests pass
- Solution builds without errors
