<!-- markdownlint-disable-file -->

# Task Details: Show Trades on Main Chart

## Phase 2: Frontend Chart Integration

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()`, `takeUntilDestroyed`, observable patterns ($-suffix for infinite), explicit accessibility, double quotes
- `.agent-context/0-knowledge/09-charting-library.md` — Overlays must be added via `ISeriesApi` instances to `PriceChartComponent`; `createSeriesMarkers` pattern
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular 19 standalone, service/observable conventions
- `.github/instructions/testing.instructions.md` — Tests within phase, MSTest-style naming adapted for Jasmine: `should + given/when/then`

## Design References

- `lightweight-charts` v4 `createSeriesMarkers` plugin API — attaches markers to an existing series, returns `ISeriesMarkersPluginApi<Time>` with `setMarkers()` method
- `lightweight-charts` v4 `subscribeCrosshairMove` API — subscribes to crosshair position changes, receives `MouseEventParams` with `.time` and `.point` for tooltip positioning

---

### Task 2.1: Expose fillEvent$ observable from SignalRService {#task-21-expose-fillevent-observable-from-signalrservice}

Add a `fillEvent$` Subject to `SignalRService` so the chart component can subscribe to real-time fill events independently from `AccountStateService`. This follows the existing `priceUpdate$` pattern.

- **Complexity**: Low
- **Risk Factors**: None — additive change, does not alter existing `AccountStateService` routing
- **Files**:
  - `frontend/trading-ui/src/app/core/services/signalr.service.ts` — Add `_fillEvent$` Subject and public `fillEvent$` observable; emit in existing `ReceiveFillEvent` handler
- **Success**:
  - `SignalRService.fillEvent$` emits each fill event as it arrives via SignalR
  - Existing `AccountStateService.addFillEvent()` call is unchanged
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/signalr.service.ts — modification

// Add alongside existing Subject declarations (e.g. _priceUpdate$)
private readonly _fillEvent$ = new Subject<FillEvent>();
public readonly fillEvent$: Observable<FillEvent> = this._fillEvent$.asObservable();

// In _registerHandlers(), modify the existing ReceiveFillEvent handler:
this._hubConnection.on("ReceiveFillEvent", (fill: FillEvent) => {
    this._accountState.addFillEvent(fill);
    this._fillEvent$.next(fill);  // ← add this line
});
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/signalr.service.ts` — existing `_priceUpdate$` / `priceUpdate$` Subject pattern

---

### Task 2.2: Update HyperliquidApiService with asset parameter {#task-22-update-hyperliquidapiservice-with-asset-parameter}

Add an optional `asset` parameter to `HyperliquidApiService.getRecentFills()` to pass through to the backend API as a query parameter.

- **Complexity**: Low
- **Risk Factors**: Must preserve backward compatibility — existing callers (ActivityFeedComponent) pass no argument
- **Files**:
  - `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — Add optional `asset?: string` parameter, append as query param when provided
- **Success**:
  - `getRecentFills()` (no args) still calls `GET /api/account/fills` (unchanged)
  - `getRecentFills("BTC-PERP")` calls `GET /api/account/fills?asset=BTC-PERP`
- **Dependencies**: Phase 1 (backend endpoint with asset param)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts — modification

// Change from:
public getRecentFills(): Observable<FillEvent[]> {
    return this._http.get<FillEvent[]>(`${this._baseUrl}/api/account/fills`);
}

// To:
public getRecentFills(asset?: string): Observable<FillEvent[]> {
    const params = asset ? `?asset=${encodeURIComponent(asset)}` : "";
    return this._http.get<FillEvent[]>(`${this._baseUrl}/api/account/fills${params}`);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — existing `getRecentFills()` method

---

### Task 2.3: Add trade markers to PriceChartComponent {#task-23-add-trade-markers-to-pricechartcomponent}

Add `createSeriesMarkers` integration to `PriceChartComponent`, matching the exact marker style from `CycleChartComponent`: green `#26a69a` arrowUp below bar for buys, amber `#f59e0b` arrowDown above bar for sells. Accept fills via `@Input()` and toggle via `@Input()`. Build markers, sort by time, and apply to the candlestick series. Re-apply markers after `setData()` calls and on `ngOnChanges`.

- **Complexity**: High
- **Risk Factors**: Markers must be re-applied after `setData()` calls in `_seedFromCandles()` and `prependCandles()`; fills outside loaded candle range must be silently excluded; asset name mismatch requires stripping `-PERP` suffix when comparing
- **Files**:
  - `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts` — Add marker imports, inputs, `_markersApi` field, `_buildFillMarkers()`, `_refreshMarkers()`, marker lifecycle management
- **Success**:
  - Buy fills render as green arrowUp markers below the candle bar
  - Sell fills render as amber arrowDown markers above the candle bar
  - Markers are sorted by time ascending
  - Toggling `showTradeMarkers` to false clears markers; toggling back restores them
  - Changing `selectedAsset` clears old markers (parent handles re-fetching fills)
  - Fills outside loaded candle range produce no markers
  - `ngOnDestroy` cleans up `_markersApi`
- **Dependencies**: Task 2.1 (fillEvent$ for real-time), Task 2.2 (API service for historical fills)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts — modification

// Add to imports:
import {
    createSeriesMarkers,
    ISeriesMarkersPluginApi,
    SeriesMarker,
    Time,
    UTCTimestamp
} from "lightweight-charts";
import { FillEvent } from "../../../core/models/fill-event.model";

// Add new @Input() declarations alongside existing ones:
@Input() public fills: FillEvent[] = [];
@Input() public showTradeMarkers = true;

// Add private fields:
private _markersApi: ISeriesMarkersPluginApi<Time> | null = null;
private _currentFills: FillEvent[] = [];

// In ngAfterViewInit, after _initChart() creates _candleSeries:
this._markersApi = createSeriesMarkers(this._candleSeries!, []);

// Extend the existing ngOnChanges handler (line 83) with additional checks for fills and showTradeMarkers:
// Add these checks BEFORE the existing seedCandles/selectedTimeframe/selectedAsset block:
if (changes["fills"]) {
    this._currentFills = this.fills;
    this._refreshMarkers();
}
if (changes["showTradeMarkers"]) {
    this._refreshMarkers();
}

// Add new private methods:

private _refreshMarkers(): void {
    if (!this._markersApi) return;

    if (!this.showTradeMarkers || this._currentFills.length === 0) {
        this._markersApi.setMarkers([]);
        return;
    }

    const markers = this._buildFillMarkers(this._currentFills);
    this._markersApi.setMarkers(markers);
}

private _buildFillMarkers(fills: FillEvent[]): SeriesMarker<Time>[] {
    return fills
        .map(fill => {
            const isBuy = fill.side === "Buy";
            return {
                time: (Math.floor(new Date(fill.timestamp).getTime() / 1000)) as UTCTimestamp,
                position: isBuy ? "belowBar" as const : "aboveBar" as const,
                color: isBuy ? "#26a69a" : "#f59e0b",
                shape: isBuy ? "arrowUp" as const : "arrowDown" as const,
                text: `${fill.side} ${fill.size} @ ${fill.price.toLocaleString("en-US", {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2
                })}`
            };
        })
        .sort((a, b) => (a.time as number) - (b.time as number));
}

// Add to public addFill method for real-time fills from SignalR:
public addFill(fill: FillEvent): void {
    this._currentFills = [...this._currentFills, fill];
    this._refreshMarkers();
}

// In ngOnDestroy, before chart.remove():
this._markersApi = null;

// IMPORTANT: Call this._refreshMarkers() after every setData() call
// in _seedFromCandles() and prependCandles() methods
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/cycle-chart/cycle-chart.component.ts` — `_buildOrderMarkers()` pattern, `createSeriesMarkers` lifecycle, marker shape/color constants
- `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts` — marker deduplication pattern, `setMarkers()` after data updates

---

### Task 2.4: Add crosshairMove tooltip overlay to PriceChartComponent {#task-24-add-crosshairmove-tooltip-overlay-to-pricechartcomponent}

Implement a custom hover tooltip for fill markers using `subscribeCrosshairMove` on the chart instance. When the crosshair is near a marker's timestamp, show an absolutely-positioned HTML overlay with side, price, size, fee, and closed PnL. This is a new pattern in the codebase.

- **Complexity**: High
- **Risk Factors**: New pattern — no existing `subscribeCrosshairMove` usage in codebase; requires careful DOM positioning and cleanup; must handle edge cases (crosshair outside chart, multiple fills at same timestamp)
- **Files**:
  - `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts` — Add `_tooltipEl`, `_fillsByTime` Map, `_subscribeCrosshairMove()`, `_showFillTooltip()`, `_hideFillTooltip()`
  - `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.html` — Add tooltip overlay div
  - `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.scss` — Add tooltip styling
- **Success**:
  - Hovering the crosshair at a fill marker's candle time shows a tooltip with side, price, size, fee, closed PnL
  - Moving the crosshair away hides the tooltip
  - Tooltip displays correctly at the crosshair position without overflowing the chart container
  - When `showTradeMarkers` is false, tooltip does not appear
- **Dependencies**: Task 2.3 (markers must exist for tooltip to reference)

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.html — modification -->
<section class="price-chart">
    <div #chartContainer class="price-chart__container"></div>
    @if (tooltipVisible && tooltipFills.length > 0) {
        <div class="price-chart__fill-tooltip"
             [style.left.px]="tooltipLeft"
             [style.top.px]="tooltipTop">
            @for (fill of tooltipFills; track fill.orderId; let last = $last) {
                <div class="tooltip-side" [class.tooltip-side--buy]="fill.side === 'Buy'" [class.tooltip-side--sell]="fill.side === 'Sell'">
                    {{ fill.side }} {{ fill.direction }}
                </div>
                <div class="tooltip-row"><span class="tooltip-label">Price</span><span>{{ fill.price | number:'1.2-2' }}</span></div>
                <div class="tooltip-row"><span class="tooltip-label">Size</span><span>{{ fill.size }}</span></div>
                <div class="tooltip-row"><span class="tooltip-label">Fee</span><span>{{ fill.fee | number:'1.4-4' }}</span></div>
                <div class="tooltip-row"><span class="tooltip-label">Closed PnL</span><span>{{ fill.closedPnl | number:'1.2-2' }}</span></div>
                @if (!last) {
                    <hr class="tooltip-divider" />
                }
            }
        </div>
    }
</section>
```

```scss
// frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.scss — addition

.price-chart__fill-tooltip {
    position: absolute;
    z-index: 10;
    padding: 8px 12px;
    background: rgba(30, 30, 30, 0.95);
    border: 1px solid rgba(255, 255, 255, 0.15);
    border-radius: 4px;
    font-size: 12px;
    line-height: 1.5;
    color: #d1d5db;
    pointer-events: none;
    white-space: nowrap;

    .tooltip-side {
        font-weight: 600;

        &--buy { color: #26a69a; }
        &--sell { color: #f59e0b; }
    }

    .tooltip-row {
        display: flex;
        justify-content: space-between;
        gap: 16px;
    }

    .tooltip-label {
        color: #9ca3af;
    }

    .tooltip-divider {
        border-color: rgba(255, 255, 255, 0.1);
        margin: 4px 0;
    }
}
```

```typescript
// frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts — modification

// No ViewChild needed for tooltip — using Angular template binding with public fields
// (tooltipFills, tooltipVisible, tooltipLeft, tooltipTop defined in _showFillTooltip section)

// Add private field for fill lookup:
private _fillsByTime = new Map<number, FillEvent[]>();

// Call this when fills change (in _refreshMarkers or ngOnChanges):
private _rebuildFillLookup(): void {
    this._fillsByTime.clear();
    for (const fill of this._currentFills) {
        const time = Math.floor(new Date(fill.timestamp).getTime() / 1000);
        const existing = this._fillsByTime.get(time) ?? [];
        existing.push(fill);
        this._fillsByTime.set(time, existing);
    }
}

// Subscribe in ngAfterViewInit, after chart creation:
// Store the handler reference as a private field for proper cleanup:
private _crosshairHandler: ((param: MouseEventParams) => void) | null = null;

private _subscribeCrosshairMove(): void {
    this._crosshairHandler = (param: MouseEventParams) => {
        if (!this.showTradeMarkers || !param.time || !param.point) {
            this._hideFillTooltip();
            return;
        }

        const fills = this._fillsByTime.get(param.time as number);
        if (!fills || fills.length === 0) {
            this._hideFillTooltip();
            return;
        }

        this._showFillTooltip(fills, param.point.x, param.point.y);
    };
    this._chart!.subscribeCrosshairMove(this._crosshairHandler);
}

// Use Angular template binding instead of innerHTML to preserve XSS sanitization.
// Add these public fields to expose tooltip state to the template:
public tooltipFills: FillEvent[] = [];
public tooltipVisible = false;
public tooltipLeft = 0;
public tooltipTop = 0;

private _showFillTooltip(fills: FillEvent[], x: number, y: number): void {
    this.tooltipFills = fills;
    this.tooltipLeft = x + 16;
    this.tooltipTop = y - 16;
    this.tooltipVisible = true;
}

private _hideFillTooltip(): void {
    this.tooltipVisible = false;
    this.tooltipFills = [];
}

// In ngOnDestroy, before chart.remove():
if (this._crosshairHandler) {
    this._chart?.unsubscribeCrosshairMove(this._crosshairHandler);
    this._crosshairHandler = null;
}
```

##### Pattern References

- `lightweight-charts` v4 API docs — `subscribeCrosshairMove(handler)` / `unsubscribeCrosshairMove(handler)`
- No existing codebase pattern — this is a new tooltip mechanism

---

### Task 2.5: Add toggle button and fill orchestration to MarketDataComponent {#task-25-add-toggle-button-and-fill-orchestration-to-marketdatacomponent}

Add a "Trades" toggle button in the Market Data page header controls. Manage fill state: fetch fills from API when asset changes, subscribe to real-time fill events from `SignalRService`, filter by selected asset, and pass fills + toggle state to `PriceChartComponent`.

- **Complexity**: Medium
- **Risk Factors**: Asset name mismatch — `FillEvent.asset` is coin ("BTC") while `selectedAsset` is display form ("BTC-PERP"); must strip `-PERP` suffix when comparing for real-time filter
- **Files**:
  - `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — Add `showFills` toggle state, fills array, API fetch on asset change, SignalR subscription for real-time fills
  - `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — Add toggle button in controls div, bind `[fills]` and `[showTradeMarkers]` to `<app-price-chart>`
- **Success**:
  - Toggle button appears in the Market Data page header controls
  - Clicking the toggle toggles `showFills` state and shows/hides markers on the chart
  - Changing the selected asset re-fetches fills for the new asset
  - Real-time fill events for the selected asset are appended to the chart
  - Real-time fill events for other assets are ignored
- **Dependencies**: Tasks 2.1, 2.2, 2.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/market-data/market-data.component.ts — modification

// Add imports (HyperliquidApiService and SignalRService must also be imported):
import { FillEvent } from "../../core/models/fill-event.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { SignalRService } from "../../core/services/signalr.service";

// Note: MatIconModule is already imported in this component — no need to add it again

// Add fields:
public showFills = true;
public fills: FillEvent[] = [];

// Inject services:
private readonly _signalRService = inject(SignalRService);
private readonly _apiService = inject(HyperliquidApiService);
private readonly _destroyRef = inject(DestroyRef);

// Add to ngOnInit or ngAfterViewInit:
private _subscribeToFillEvents(): void {
    this._signalRService.fillEvent$
        .pipe(takeUntilDestroyed(this._destroyRef))
        .subscribe((fill: FillEvent) => {
            const coin = this.selectedAsset.replace("-PERP", "");
            if (fill.asset === coin) {
                this.fills = [...this.fills, fill];
                this._priceChart?.addFill(fill);
            }
        });
}

// In onAssetChanged (existing method), after candle re-fetch:
private _loadFillsForAsset(asset: string): void {
    this._apiService.getRecentFills(asset).subscribe(fills => {
        this.fills = fills;
    });
}

// Add toggle handler:
public onToggleFills(): void {
    this.showFills = !this.showFills;
}
```

```html
<!-- frontend/trading-ui/src/app/features/market-data/market-data.component.html — modification -->

<!-- Add toggle button alongside existing controls (e.g. after Refresh button): -->
<button mat-stroked-button type="button" (click)="onToggleFills()"
        [class.active]="showFills">
    <mat-icon>{{ showFills ? "visibility" : "visibility_off" }}</mat-icon>
    Trades
</button>

<!-- Update app-price-chart binding: -->
<app-price-chart
    [seedCandles]="candles"
    [selectedAsset]="selectedAsset"
    [selectedTimeframe]="selectedTimeframe"
    [fills]="fills"
    [showTradeMarkers]="showFills"
    (loadMoreCandles)="onLoadMoreCandles($event)"
/>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — existing `onAssetChanged` and `_priceChart` ViewChild pattern
- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — existing controls div with `mat-raised-button`

---

### Task 2.6: Add PriceChartComponent spec for marker rendering {#task-26-add-pricechartcomponent-spec-for-marker-rendering}

Create a test spec for `PriceChartComponent` covering marker rendering, toggle behavior, and real-time fill appending. Follow the `EquityChartComponent` spec pattern with `ResizeObserverMock`.

- **Complexity**: Medium
- **Risk Factors**: `PriceChartComponent` has no existing spec — must create from scratch; chart instance is private and requires cast to access
- **Files**:
  - `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.spec.ts` — new file
- **Success**:
  - Test: "should create markers for buy and sell fills" — verifies `_markersApi` is initialized
  - Test: "should hide markers when showTradeMarkers is false" — verifies markers cleared
  - Test: "should add real-time fill via addFill()" — verifies marker count increases
  - Test: "should clear markers when fills input is empty" — verifies clean state
  - All tests pass via `ng test`
- **Dependencies**: Tasks 2.3, 2.4, 2.5

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.spec.ts — new file

import { ComponentFixture, TestBed } from "@angular/core/testing";
import { PriceChartComponent } from "./price-chart.component";
import { FillEvent } from "../../../core/models/fill-event.model";
import { SignalRService } from "../../../core/services/signalr.service";
import { Subject } from "rxjs";

class ResizeObserverMock {
    public observe = jasmine.createSpy("observe");
    public unobserve = jasmine.createSpy("unobserve");
    public disconnect = jasmine.createSpy("disconnect");
}

describe("PriceChartComponent", () => {
    let component: PriceChartComponent;
    let fixture: ComponentFixture<PriceChartComponent>;
    let originalResizeObserver: typeof ResizeObserver;

    const fillSeed: FillEvent[] = [
        {
            timestamp: "2026-03-30T10:00:00Z",
            asset: "BTC",
            side: "Buy",
            direction: "Open Long",
            size: 0.1,
            price: 65000,
            fee: 0.01,
            closedPnl: 0,
            orderId: "order-1"
        },
        {
            timestamp: "2026-03-30T11:00:00Z",
            asset: "BTC",
            side: "Sell",
            direction: "Close Long",
            size: 0.1,
            price: 66000,
            fee: 0.01,
            closedPnl: 100,
            orderId: "order-2"
        }
    ];

    beforeEach(async () => {
        originalResizeObserver = globalThis.ResizeObserver;
        globalThis.ResizeObserver = ResizeObserverMock as never;

        const signalRMock = {
            priceUpdate$: new Subject(),
            candleUpdate$: new Subject(),
            fillEvent$: new Subject()
        };

        await TestBed.configureTestingModule({
            imports: [PriceChartComponent],
            providers: [
                { provide: SignalRService, useValue: signalRMock }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(PriceChartComponent);
        component = fixture.componentInstance;
    });

    afterEach(() => {
        globalThis.ResizeObserver = originalResizeObserver;
    });

    it("should create the component", () => {
        fixture.detectChanges();
        expect(component).toBeTruthy();
    });

    it("should initialize markers API when chart is created", () => {
        fixture.detectChanges();
        const internal = component as unknown as { _markersApi: unknown };
        expect(internal._markersApi).toBeTruthy();
    });

    it("should build markers for fill inputs", () => {
        fixture.componentRef.setInput("fills", fillSeed);
        fixture.componentRef.setInput("showTradeMarkers", true);
        fixture.detectChanges();
        // Markers are applied — component should have stored fills
        const internal = component as unknown as { _currentFills: FillEvent[] };
        expect(internal._currentFills.length).toBe(2);
    });

    it("should clear markers when showTradeMarkers is false", () => {
        fixture.componentRef.setInput("fills", fillSeed);
        fixture.componentRef.setInput("showTradeMarkers", false);
        fixture.detectChanges();
        // Component should still have fills but markers not rendered
        expect(component.showTradeMarkers).toBeFalse();
    });

    it("should add real-time fill via addFill()", () => {
        fixture.componentRef.setInput("fills", fillSeed);
        fixture.detectChanges();

        const newFill: FillEvent = {
            timestamp: "2026-03-30T12:00:00Z",
            asset: "BTC",
            side: "Buy",
            direction: "Open Long",
            size: 0.2,
            price: 66500,
            fee: 0.02,
            closedPnl: 0,
            orderId: "order-3"
        };
        component.addFill(newFill);

        const internal = component as unknown as { _currentFills: FillEvent[] };
        expect(internal._currentFills.length).toBe(3);
    });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.spec.ts` — `ResizeObserverMock`, `setInput()`, private field access via cast
- `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.spec.ts` — `FillEvent` seed data, mock service providers

---

### Task 2.7: Build and lint frontend {#task-27-build-and-lint-frontend}

Run the frontend build and lint to verify all changes compile and pass linting.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No file changes
- **Success**:
  - `npm run build` succeeds with no errors
  - `npm run lint` passes with no errors
  - `ng test --watch=false` passes all tests including the new spec
- **Dependencies**: Tasks 2.1–2.6

## Phase Success Criteria

- Buy fills render as green `#26a69a` arrowUp markers below candles on the price chart
- Sell fills render as amber `#f59e0b` arrowDown markers above candles
- Real-time fills from SignalR add markers without page refresh
- Toggle button in Market Data controls shows/hides markers
- Hovering near a marker shows tooltip with side, price, size, fee, and closed PnL
- Switching assets re-fetches and re-renders markers for the new asset
- No markers for assets that have no fills; no markers for fills outside loaded candle range
- Frontend builds, lints, and all tests pass
