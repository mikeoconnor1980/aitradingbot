<!-- markdownlint-disable-file -->

# Task Details: F3 — Market Data (REST)

## Phase 2: Frontend — Angular Market Data Page

## Standards and Knowledge References

- **angular.instructions.md**: Service-based HTTP calls via ApiRestClient wrapper; `providedIn: "root"` services; explicit `public`/`private` on all members; `_camelCase` for private fields; explicit return types; double-quoted strings; SCSS only; DestroyRef + takeUntilDestroyed for infinite observables (polling)
- **POC spec (hyperlink-poc.md)**: Angular 19 standalone components; feature folder at `features/market-data/`; models at `core/models/market.model.ts`; services at `core/services/`
- **F3 PBI**: Asset selector dropdown (hardcoded: BTC-PERP default), market info card (mid, mark, index, funding, volume, OI, 24h change), timeframe selector (15m default, 1H, 4H), candle table (50 rows, newest first, OHLCV), manual refresh button, 10s auto-poll for market info only
- **User decisions**: Angular Material for UI, standalone components, ApiRestClient wrapper

## Design References

- Angular Material components: `mat-select` for dropdowns, `mat-card` for market info card, `mat-table` for candle data, `mat-button` for refresh
- Polling: `interval(10000)` with `startWith(0)` → `switchMap` → `takeUntilDestroyed(destroyRef)` for market info auto-refresh
- Candle table: no polling, refreshes only on timeframe/asset change or manual button press

---

### Task 2.1: Install Angular Material and create ApiRestClient wrapper {#task-21-install-angular-material-and-create-apirestclient-wrapper}

Install Angular Material and create a reusable ApiRestClient service that wraps HttpClient with base URL configuration.

- **Complexity**: Medium
- **Risk Factors**: Angular Material schematics may modify app configuration; ApiRestClient must correctly construct URLs relative to the API base
- **Files**:
  - `frontend/hyperliquid-poc/package.json` — modification: add @angular/material
  - `frontend/hyperliquid-poc/src/app/core/services/api-rest-client.service.ts` — New: generic HTTP wrapper
  - `frontend/hyperliquid-poc/src/app/app.config.ts` — modification: add provideHttpClient and provideAnimations
  - `frontend/hyperliquid-poc/src/styles.scss` — modification: add Angular Material theme import
- **Success**:
  - `npm install` succeeds with Angular Material
  - ApiRestClient provides typed get/post/put/delete methods
  - Angular Material theme renders correctly
- **Dependencies**:
  - F1 must have created the Angular app scaffold

#### Implementation Details

```bash
# Install Angular Material in the frontend project
cd frontend/hyperliquid-poc
ng add @angular/material --skip-confirmation
```

```typescript
// frontend/hyperliquid-poc/src/app/core/services/api-rest-client.service.ts — new file
import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

@Injectable({ providedIn: "root" })
export class ApiRestClient {
    private readonly _httpClient: HttpClient;
    private readonly _baseUrl: string;

    public constructor(httpClient: HttpClient) {
        this._httpClient = httpClient;
        this._baseUrl = environment.apiBaseUrl;
    }

    public get<T>(path: string): Observable<T> {
        return this._httpClient.get<T>(`${this._baseUrl}/${path}`);
    }

    public post<T>(path: string, body: unknown): Observable<T> {
        return this._httpClient.post<T>(`${this._baseUrl}/${path}`, body);
    }

    public put<T>(path: string, body: unknown): Observable<T> {
        return this._httpClient.put<T>(`${this._baseUrl}/${path}`, body);
    }

    public delete<T>(path: string): Observable<T> {
        return this._httpClient.delete<T>(`${this._baseUrl}/${path}`);
    }
}
```

```typescript
// frontend/hyperliquid-poc/src/environments/environment.ts — new file (or modification)
export const environment = {
    production: false,
    apiBaseUrl: "http://localhost:5000/api"  // Must NOT end with trailing slash. Verify port matches F1's API configuration.
};
```

```typescript
// frontend/hyperliquid-poc/src/environments/environment.prod.ts — new file (or modification)
export const environment = {
    production: true,
    apiBaseUrl: "/api"
};
```

> **Note**: If F1 already configured a proxy (`proxy.conf.json`), adjust the `apiBaseUrl` accordingly. The proxy would forward `/api/*` to the .NET backend, so `apiBaseUrl` could be `/api` in development too.

##### Pattern References

- `angular.instructions.md` — ApiRestClient pattern, providedIn: "root", private readonly fields with `_` prefix

---

### Task 2.2: Create market data models and DTOs {#task-22-create-market-data-models-and-dtos}

Create TypeScript interfaces matching the backend DTOs.

- **Complexity**: Low
- **Risk Factors**: None — straightforward interface definitions
- **Files**:
  - `frontend/hyperliquid-poc/src/app/core/models/market-info.model.ts` — MarketInfo interface
  - `frontend/hyperliquid-poc/src/app/core/models/candle.model.ts` — Candle interface
- **Success**:
  - Interfaces match backend DTO property names exactly (camelCase in JSON)
  - No `any` types used
- **Dependencies**:
  - None

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/core/models/market-info.model.ts — new file
export interface MarketInfo {
    asset: string;
    midPrice: number;
    markPrice: number;
    indexPrice: number;
    fundingRate: number;
    volume24h: number;
    openInterest: number;
    priceChange24hPercent: number;
}
```

```typescript
// frontend/hyperliquid-poc/src/app/core/models/candle.model.ts — new file
export interface Candle {
    timestamp: number;  // Unix milliseconds
    open: number;
    high: number;
    low: number;
    close: number;
    volume: number;
}
```

##### Pattern References

- `angular.instructions.md` — Models in `core/models/` with `.model.ts` suffix, interfaces (not classes) for DTOs

---

### Task 2.3: Create market data API service {#task-23-create-market-data-api-service}

Create an Angular service that uses the ApiRestClient to call the market data endpoints.

- **Complexity**: Low
- **Risk Factors**: URL path construction must match backend routes exactly
- **Files**:
  - `frontend/hyperliquid-poc/src/app/core/services/market-data.service.ts` — New service
- **Success**:
  - `getMarketInfo(asset)` calls `GET market/info?asset={asset}`
  - `getCandles(asset, timeframe)` calls `GET market/candles?asset={asset}&timeframe={timeframe}`
  - Methods return typed Observables
- **Dependencies**:
  - Task 2.1 (ApiRestClient)
  - Task 2.2 (Models)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/core/services/market-data.service.ts — new file
import { Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import { MarketInfo } from "../models/market-info.model";
import { Candle } from "../models/candle.model";

@Injectable({ providedIn: "root" })
export class MarketDataService {
    private readonly _apiClient: ApiRestClient;

    public constructor(apiClient: ApiRestClient) {
        this._apiClient = apiClient;
    }

    public getMarketInfo(asset: string): Observable<MarketInfo> {
        return this._apiClient.get<MarketInfo>(`market/info?asset=${encodeURIComponent(asset)}`);
    }

    public getCandles(asset: string, timeframe: string): Observable<Candle[]> {
        return this._apiClient.get<Candle[]>(
            `market/candles?asset=${encodeURIComponent(asset)}&timeframe=${encodeURIComponent(timeframe)}`
        );
    }
}
```

##### Pattern References

- `angular.instructions.md` — Service with ApiRestClient injection, typed Observable returns, providedIn: "root"

---

### Task 2.4: Create market data page component {#task-24-create-market-data-page-component}

Create the main market data page component with asset selector, market info card, timeframe selector, candle table, and refresh button.

- **Complexity**: High
- **Risk Factors**: Complex component with multiple interactive elements; Angular Material integration; proper data binding and change handling
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.ts` — Component class
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.html` — Template
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.scss` — Styles
- **Success**:
  - Asset dropdown shows hardcoded list with BTC-PERP as default
  - Market info card displays all 7 fields (mid, mark, index, funding, volume, OI, 24h change)
  - Timeframe selector shows 15m (default), 1H, 4H
  - Candle table shows 50 rows with timestamp, open, high, low, close, volume columns
  - Refresh button visible and functional
  - Selecting a different asset reloads both info and candles
  - Selecting a different timeframe reloads candles only
- **Dependencies**:
  - Task 2.1 (Angular Material)
  - Task 2.2 (Models)
  - Task 2.3 (MarketDataService)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.ts — new file
import { Component, OnInit, DestroyRef } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatSelectModule } from "@angular/material/select";
import { MatCardModule } from "@angular/material/card";
import { MatTableModule } from "@angular/material/table";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatFormFieldModule } from "@angular/material/form-field";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { interval, switchMap, startWith, Subject, merge } from "rxjs";
import { MarketDataService } from "../../core/services/market-data.service";
import { MarketInfo } from "../../core/models/market-info.model";
import { Candle } from "../../core/models/candle.model";

@Component({
    selector: "app-market-data",
    standalone: true,
    imports: [
        CommonModule,
        MatSelectModule,
        MatCardModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatProgressBarModule,
        MatFormFieldModule,
    ],
    templateUrl: "./market-data.component.html",
    styleUrls: ["./market-data.component.scss"],
})
export class MarketDataComponent implements OnInit {
    private readonly _marketDataService: MarketDataService;
    private readonly _destroyRef: DestroyRef;
    private readonly _manualRefresh$ = new Subject<void>();

    public readonly assets: string[] = [
        "BTC-PERP", "ETH-PERP", "SOL-PERP", "DOGE-PERP",
        "AVAX-PERP", "ARB-PERP", "LINK-PERP", "OP-PERP"
    ];
    public readonly timeframes: string[] = ["15m", "1H", "4H"];
    public readonly candleColumns: string[] = ["timestamp", "open", "high", "low", "close", "volume"];

    public selectedAsset: string = "BTC-PERP";
    public selectedTimeframe: string = "15m";
    public marketInfo: MarketInfo | null = null;
    public candles: Candle[] = [];
    public marketInfoError: string | null = null;
    public candleError: string | null = null;
    public isLoadingMarketInfo: boolean = false;
    public isLoadingCandles: boolean = false;

    public constructor(marketDataService: MarketDataService, destroyRef: DestroyRef) {
        this._marketDataService = marketDataService;
        this._destroyRef = destroyRef;
    }

    public ngOnInit(): void {
        this._startMarketInfoPolling();
        this._loadCandles();
    }

    public onAssetChanged(asset: string): void {
        this.selectedAsset = asset;
        this.marketInfo = null;
        this.candles = [];
        this._startMarketInfoPolling();
        this._loadCandles();
    }

    public onTimeframeChanged(timeframe: string): void {
        this.selectedTimeframe = timeframe;
        this._loadCandles();
    }

    public onManualRefresh(): void {
        this._manualRefresh$.next();
        this._loadCandles();
    }

    private _startMarketInfoPolling(): void {
        // Market info auto-refreshes every 10 seconds
        merge(
            interval(10000),
            this._manualRefresh$
        ).pipe(
            startWith(0),
            takeUntilDestroyed(this._destroyRef),
            switchMap(() => {
                this.isLoadingMarketInfo = true;
                return this._marketDataService.getMarketInfo(this.selectedAsset);
            })
        ).subscribe({
            next: (data: MarketInfo) => {
                this.marketInfo = data;
                this.marketInfoError = null;
                this.isLoadingMarketInfo = false;
            },
            error: (err: unknown) => {
                this.marketInfoError = "Failed to load market data. Will retry on next poll cycle.";
                this.isLoadingMarketInfo = false;
            }
        });
    }

    private _loadCandles(): void {
        this.isLoadingCandles = true;
        this.candleError = null;
        this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe)
            .subscribe({
                next: (data: Candle[]) => {
                    this.candles = data;
                    this.candleError = null;
                    this.isLoadingCandles = false;
                },
                error: (err: unknown) => {
                    this.candleError = "Failed to load candle data.";
                    this.candles = [];
                    this.isLoadingCandles = false;
                }
            });
    }
}
```

```html
<!-- frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.html — new file -->
<div class="market-data-page">
    <div class="page-header">
        <h1>Market Data</h1>
        <div class="controls">
            <mat-form-field appearance="outline">
                <mat-label>Asset</mat-label>
                <mat-select [value]="selectedAsset" (selectionChange)="onAssetChanged($event.value)">
                    @for (asset of assets; track asset) {
                        <mat-option [value]="asset">{{ asset }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>

            <button mat-raised-button color="primary" (click)="onManualRefresh()">
                <mat-icon>refresh</mat-icon>
                Refresh
            </button>
        </div>
    </div>

    <!-- Market Info Card -->
    <mat-card class="market-info-card">
        <mat-card-header>
            <mat-card-title>{{ selectedAsset }} Market Info</mat-card-title>
        </mat-card-header>
        @if (isLoadingMarketInfo && !marketInfo) {
            <mat-progress-bar mode="indeterminate"></mat-progress-bar>
        }
        @if (marketInfoError) {
            <mat-card-content>
                <div class="error-banner">{{ marketInfoError }}</div>
            </mat-card-content>
        }
        @if (marketInfo) {
            <mat-card-content>
                <div class="info-grid">
                    <div class="info-item">
                        <span class="label">Mid Price</span>
                        <span class="value">{{ marketInfo.midPrice | number:'1.2-2' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">Mark Price</span>
                        <span class="value">{{ marketInfo.markPrice | number:'1.2-2' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">Index Price</span>
                        <span class="value">{{ marketInfo.indexPrice | number:'1.2-2' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">Funding Rate</span>
                        <span class="value">{{ marketInfo.fundingRate | number:'1.6-6' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">24h Volume</span>
                        <span class="value">{{ marketInfo.volume24h | number:'1.0-0' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">Open Interest</span>
                        <span class="value">{{ marketInfo.openInterest | number:'1.0-0' }}</span>
                    </div>
                    <div class="info-item">
                        <span class="label">24h Change</span>
                        <span class="value" [class.positive]="marketInfo.priceChange24hPercent > 0" [class.negative]="marketInfo.priceChange24hPercent < 0">
                            {{ marketInfo.priceChange24hPercent | number:'1.2-2' }}%
                        </span>
                    </div>
                </div>
            </mat-card-content>
        }
        @if (!marketInfo && !marketInfoError && !isLoadingMarketInfo) {
            <mat-card-content>
                <div class="empty-state">Asset not available</div>
            </mat-card-content>
        }
    </mat-card>

    <!-- Candle Data Section -->
    <div class="candle-section">
        <div class="candle-header">
            <h2>Candle Data</h2>
            <mat-form-field appearance="outline">
                <mat-label>Timeframe</mat-label>
                <mat-select [value]="selectedTimeframe" (selectionChange)="onTimeframeChanged($event.value)">
                    @for (tf of timeframes; track tf) {
                        <mat-option [value]="tf">{{ tf }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>
        </div>

        @if (isLoadingCandles) {
            <mat-progress-bar mode="indeterminate"></mat-progress-bar>
        }
        @if (candleError) {
            <div class="error-banner">{{ candleError }}</div>
        }
        @if (candles.length > 0) {
            <table mat-table [dataSource]="candles" class="candle-table">
                <ng-container matColumnDef="timestamp">
                    <th mat-header-cell *matHeaderCellDef>Time</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.timestamp | date:'short' }}</td>
                </ng-container>
                <ng-container matColumnDef="open">
                    <th mat-header-cell *matHeaderCellDef>Open</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.open | number:'1.2-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="high">
                    <th mat-header-cell *matHeaderCellDef>High</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.high | number:'1.2-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="low">
                    <th mat-header-cell *matHeaderCellDef>Low</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.low | number:'1.2-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="close">
                    <th mat-header-cell *matHeaderCellDef>Close</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.close | number:'1.2-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="volume">
                    <th mat-header-cell *matHeaderCellDef>Volume</th>
                    <td mat-cell *matCellDef="let candle">{{ candle.volume | number:'1.0-0' }}</td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="candleColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: candleColumns;"></tr>
            </table>
        }
        @if (candles.length === 0 && !candleError && !isLoadingCandles) {
            <div class="empty-state">No candle data available</div>
        }
    </div>
</div>
```

```scss
// frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.scss — new file
.market-data-page {
    padding: 24px;
    max-width: 1200px;
    margin: 0 auto;
}

.page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;

    h1 {
        margin: 0;
    }

    .controls {
        display: flex;
        gap: 16px;
        align-items: center;
    }
}

.market-info-card {
    margin-bottom: 24px;
}

.info-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
    gap: 16px;
    padding: 16px 0;
}

.info-item {
    display: flex;
    flex-direction: column;

    .label {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
        text-transform: uppercase;
        letter-spacing: 0.5px;
    }

    .value {
        font-size: 18px;
        font-weight: 500;
        margin-top: 4px;

        &.positive {
            color: #4caf50;
        }

        &.negative {
            color: #f44336;
        }
    }
}

.candle-section {
    .candle-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 16px;

        h2 {
            margin: 0;
        }
    }
}

.candle-table {
    width: 100%;
}

.error-banner {
    background-color: #ffebee;
    color: #c62828;
    padding: 12px 16px;
    border-radius: 4px;
    margin: 8px 0;
}

.empty-state {
    text-align: center;
    padding: 32px;
    color: rgba(0, 0, 0, 0.6);
    font-style: italic;
}
```

> **Note**: The template uses Angular 17+ `@for` control flow syntax. The `date` pipe on `candle.timestamp` expects a number (Unix ms) which Angular's DatePipe handles automatically. If the timestamp is in seconds, multiply by 1000 first.

##### Pattern References

- `angular.instructions.md` — Component structure with explicit access modifiers, constructor injection, DestroyRef
- POC spec (`hyperlink-poc.md`) — Feature folder at `features/market-data/`

---

### Task 2.5: Configure routing and navigation {#task-25-configure-routing-and-navigation}

Add the market data route to the app's routing configuration and provide navigation to the page.

- **Complexity**: Low
- **Risk Factors**: Must not break existing routes from F1
- **Files**:
  - `frontend/hyperliquid-poc/src/app/app.routes.ts` — modification: add market-data route
  - `frontend/hyperliquid-poc/src/app/app.component.html` — modification: add navigation link
- **Success**:
  - `/market-data` navigates to the MarketDataComponent
  - A navigation link is visible in the app layout
  - Existing routes (e.g., health status from F1) still work
- **Dependencies**:
  - Task 2.4 (MarketDataComponent)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/app.routes.ts — modification
// Add market-data route alongside existing routes
import { Routes } from "@angular/router";

export const routes: Routes = [
    // ... existing routes from F1 ...
    {
        path: "market-data",
        loadComponent: () => import("./features/market-data/market-data.component")
            .then(m => m.MarketDataComponent),
        title: "Market Data"
    },
    // ... existing default/fallback routes ...
];
```

##### Pattern References

- `angular.instructions.md` — Lazy-loaded standalone component routes
- POC spec — `features/market-data/` component path

---

### Task 2.6: Implement 10-second market info polling and manual refresh {#task-26-implement-polling-and-manual-refresh}

Verify and refine the polling implementation from Task 2.4. Ensure polling restarts correctly on asset change and manual refresh integrates with the polling cycle.

- **Complexity**: Medium
- **Risk Factors**: Polling subscription leak on asset change; manual refresh must not create duplicate subscriptions; `takeUntilDestroyed` must prevent leaks on component destroy
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.ts` — modification: refine polling lifecycle
- **Success**:
  - Market info card updates every 10 seconds without UI blocking
  - Manual refresh triggers immediate update
  - Switching assets cancels old polling and starts new
  - Candle table does NOT auto-refresh
  - No memory leaks (observable properly cleaned up)
- **Dependencies**:
  - Task 2.4 (initial component implementation)

#### Implementation Details

The initial implementation in Task 2.4 has a potential subscription leak: calling `_startMarketInfoPolling()` on asset change creates a new subscription without cancelling the old one. The refined approach uses a `BehaviorSubject` for the selected asset to drive a single polling pipeline:

```typescript
// Refinement to market-data.component.ts
// Replace the _startMarketInfoPolling approach with a reactive asset-driven pipeline:

private readonly _selectedAsset$ = new BehaviorSubject<string>("BTC-PERP");
private readonly _manualRefresh$ = new Subject<void>();

public ngOnInit(): void {
    // Single polling subscription that reacts to asset changes
    this._selectedAsset$.pipe(
        switchMap((asset: string) =>
            merge(
                interval(10000).pipe(startWith(0)),
                this._manualRefresh$
            ).pipe(
                switchMap(() => {
                    this.isLoadingMarketInfo = true;
                    return this._marketDataService.getMarketInfo(asset);
                })
            )
        ),
        takeUntilDestroyed(this._destroyRef)
    ).subscribe({
        next: (data: MarketInfo) => {
            this.marketInfo = data;
            this.marketInfoError = null;
            this.isLoadingMarketInfo = false;
        },
        error: (err: unknown) => {
            this.marketInfoError = "Failed to load market data. Will retry on next poll cycle.";
            this.isLoadingMarketInfo = false;
        }
    });

    this._loadCandles();
}

public onAssetChanged(asset: string): void {
    this.selectedAsset = asset;
    this.marketInfo = null;
    this.candles = [];
    this._selectedAsset$.next(asset);  // Triggers polling restart via switchMap
    this._loadCandles();
}
```

> **Note**: The outer `switchMap` on `_selectedAsset$` automatically unsubscribes from the previous asset's polling when a new asset is selected — no manual subscription management needed.

##### Pattern References

- `angular.instructions.md` — takeUntilDestroyed for infinite observables, switchMap for cancellation

---

### Task 2.7: Implement error states and empty states {#task-27-implement-error-states-and-empty-states}

Verify and refine error handling and empty state UI from Task 2.4.

- **Complexity**: Low
- **Risk Factors**: Error observable in polling should not complete the entire stream
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.ts` — modification: add catchError to prevent stream completion
  - `frontend/hyperliquid-poc/src/app/features/market-data/market-data.component.html` — verification: error/empty states render correctly
- **Success**:
  - API unreachable: error banner on market info card, retries on next 10s cycle
  - No candle data: "No candle data available" message
  - Asset not found: "Asset not available" message
  - Network timeout on candle fetch: error message with retry via manual refresh
  - Errors do not break the polling subscription
- **Dependencies**:
  - Task 2.6 (polling refinement)

#### Implementation Details

```typescript
// Add catchError to the polling pipeline to prevent stream termination on error:
import { catchError, of } from "rxjs";

// Inside the polling pipeline:
switchMap(() => {
    this.isLoadingMarketInfo = true;
    return this._marketDataService.getMarketInfo(asset).pipe(
        catchError((err: unknown) => {
            this.marketInfoError = "Failed to load market data. Will retry on next poll cycle.";
            this.isLoadingMarketInfo = false;
            return of(null);  // Emit null to keep the stream alive
        })
    );
})
```

The key change is wrapping the inner HTTP call with `catchError` so errors are caught per-poll-cycle rather than terminating the entire subscription.

##### Pattern References

- `angular.instructions.md` — Error handling in observable pipelines
- F3 PBI — Error state specifications

---

### Task 2.8: Build and lint verification {#task-28-build-and-lint-verification}

Run Angular build and lint to verify the frontend compiles and meets quality standards.

- **Complexity**: Low
- **Risk Factors**: Angular Material import resolution; SCSS compilation
- **Files**: None (verification only)
- **Success**:
  - `ng build` succeeds with no errors
  - `ng lint` passes (if ESLint configured)
  - No TypeScript errors
- **Dependencies**:
  - Tasks 2.1–2.7

```bash
cd frontend/hyperliquid-poc
ng build --configuration=development
ng lint   # If ESLint is configured
```

## Phase Success Criteria

- Angular Material installed and configured with theme
- ApiRestClient wrapper created and functional
- MarketDataService calls both backend endpoints with typed responses
- Market data page displays asset selector (BTC-PERP default), market info card, timeframe selector, candle table
- 10-second market info polling works without memory leaks
- Manual refresh reloads both market info and candles
- Switching asset reloads both; switching timeframe reloads candles only
- Error states show meaningful messages; empty states show "no data" messages
- `ng build` and `ng lint` pass with no errors
