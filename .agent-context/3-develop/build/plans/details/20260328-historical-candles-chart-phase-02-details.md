<!-- markdownlint-disable-file -->

# Phase 2 Details: Frontend — Wire Chart to Local DB Endpoint

## Standards & Knowledge References

- `.github/instructions/angular.instructions.md` — Angular coding standards
- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — Existing service pattern
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — Parent component with candle loading

---

## Task 2.1: Add `getHistoricalCandles()` method to `MarketDataService`

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| Modified | `frontend/trading-ui/src/app/core/services/market-data.service.ts` |

### Implementation Details

Add a new method alongside the existing `getCandles()`:

```typescript
public getHistoricalCandles(
  asset: string,
  timeframe: string,
  endTime?: number,
  limit: number = 500
): Observable<Candle[]> {
  let url = `market/candles/history?asset=${encodeURIComponent(asset)}&timeframe=${encodeURIComponent(timeframe)}&limit=${limit}`;
  if (endTime != null) {
    url += `&endTime=${endTime}`;
  }
  return this._apiClient.get<Candle[]>(url);
}
```

### Pattern Reference

Follow the exact pattern of the existing `getCandles()` method. Same encoding, same `ApiRestClient` usage.

### Success Criteria

- Method compiles and is available for injection
- Calls `GET /api/market/candles/history` with correct query parameters
- Returns `Observable<Candle[]>` (same shape as existing method)

---

## Task 2.2: Update `MarketDataComponent` to load initial candles from history endpoint

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| Modified | `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` |

### Implementation Details

Change `_startCandleLoading()` to call `getHistoricalCandles()` instead of `getCandles()` for the initial chart load:

**Before:**
```typescript
return this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe).pipe(...)
```

**After:**
```typescript
return this._marketDataService.getHistoricalCandles(this.selectedAsset, this.selectedTimeframe).pipe(...)
```

This fetches the last 500 candles from the local DB on initial load (default limit). Everything else stays the same — the `candles` array is set, `PriceChartComponent` receives it via `[seedCandles]`, and the chart renders.

### Success Criteria

- Initial chart load sources data from `GET /api/market/candles/history`
- Chart displays historical candles from the local database
- Candle table below the chart also shows historical data
- Loading states and error handling continue to work

---

## Task 2.3: Update `onLoadMoreCandles` to use history endpoint with DB fallback

**Complexity**: Low | **Risk**: Low

### Files

| Action | File |
|--------|------|
| Modified | `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` |

### Implementation Details

Change `onLoadMoreCandles()` to call the history endpoint instead of the Hyperliquid endpoint for scrollback:

**Before:**
```typescript
public onLoadMoreCandles(endTimeMs: number): void {
  this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe, endTimeMs).subscribe({
    next: (candles) => this._priceChart?.prependCandles(candles),
    error: () => this._priceChart?.prependCandles([]),
  });
}
```

**After:**
```typescript
public onLoadMoreCandles(endTimeMs: number): void {
  this._marketDataService.getHistoricalCandles(this.selectedAsset, this.selectedTimeframe, endTimeMs).subscribe({
    next: (candles) => this._priceChart?.prependCandles(candles),
    error: () => this._priceChart?.prependCandles([]),
  });
}
```

The `PriceChartComponent` already emits `loadMoreCandles` with the oldest visible candle's timestamp when the user scrolls to the left edge. The history endpoint uses `endTime` as the upper bound and returns the previous `limit` candles. `prependCandles()` handles deduplication and chart update.

### Success Criteria

- Scrolling backward in the chart loads older candles from the local DB
- No calls to the Hyperliquid API for historical scrollback
- Chart renders smoothly without flicker or duplicates
- When no more data is available, scrolling stops gracefully (empty array returned)

---

## Task 2.4: Manual smoke test — verify chart loads with historical data

**Complexity**: Low | **Risk**: Low

### Implementation Details

1. Ensure the local SQLite DB has BTC candle data (run ingestion if needed)
2. Start the API (`dotnet run`)
3. Start the UI (`ng serve`)
4. Navigate to Market Data page
5. Verify the chart shows candles going back further than ~10 days
6. Scroll the chart to the left — verify older candles load progressively
7. Verify live price updates still work (SignalR tick updates)
8. Switch timeframes — verify chart reloads with correct data
9. Switch assets — verify chart reloads (empty if no data for that asset)

### Success Criteria

- Chart displays historical candles from local DB
- Scrollback works with progressive loading
- Live updates continue functioning
- No console errors
- No regressions in existing functionality
