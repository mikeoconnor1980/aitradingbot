<!-- markdownlint-disable-file -->

# Task Details: Backtest UI Dashboard (F5)

## Phase 2: Frontend — Foundation & Navigation

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, `ApiRestClient`, model naming (`.model.ts`), service naming, `providedIn: "root"`, lazy routes
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular 19, `loadComponent`, BehaviorSubject, AsyncPipe, CSS custom properties
- `.agent-context/0-knowledge/09-charting-library.md` — lightweight-charts v5 setup patterns

## Design References

- API endpoints consumed: POST /api/backtests, GET /api/backtests/{id}, GET /api/backtests/validate, GET /api/backtests (paginated list)
- Existing service pattern: `ApiRestClient` wraps `HttpClient` with `environment.apiBaseUrl`
- Existing route pattern: `loadComponent` lazy-loading in `app.routes.ts`
- Existing nav pattern: `routerLink` anchors in `app.component.html`

### Task 2.1: Create backtest TypeScript models {#task-21-create-backtest-typescript-models}

Create all TypeScript interfaces needed for the backtest feature including request, response, and domain models.

- **Complexity**: Medium
- **Risk Factors**: Must match the backend API contract shapes exactly
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — new file
- **Success**:
  - All interfaces defined: `BacktestRequest`, `BacktestResult`, `BacktestTrade`, `EquitySnapshot`, `BacktestSummary`, `PagedResult`, `CoverageReport`, `IntervalCoverage`
  - TypeScript compiles without errors

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — new file

export interface BacktestRequest {
  symbol: string;
  intervals: string[];
  startDateUtc: number;
  endDateUtc: number;
  initialCapital: number;
  feeModel: FeeModel;
  warmupPeriod: number;
  strategyConfigJson: string;
}

export interface FeeModel {
  makerFeeRate: number;
  takerFeeRate: number;
  slippageRate: number;
}

// Note: `id` and `config` come from the BacktestRun entity wrapper (created in F4),
// not from the C# BacktestResult model directly. The API response maps BacktestRun.Id → id
// and BacktestRun.Config → config.
export interface BacktestResult {
  id: string;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  totalPnL: number;
  maxDrawdownAbsolute: number;
  maxDrawdownPercent: number;
  averageTradePnL: number;
  averageHoldTime: string;
  hedgesOpened: number;
  totalFeesPaid: number;
  gridCycles: number;
  finalEquity: number;
  equityTimeSeries: EquitySnapshot[];
  tradeLog: BacktestTrade[];
  config: BacktestRequest;
}

export interface EquitySnapshot {
  timestampUtc: number;
  equity: number;
}

// Note: `side` and `tradeType` are C# enums (OrderSide, TradeType) serialised as strings
// via JsonStringEnumConverter. Ensure the API project has string enum serialisation configured.
export interface BacktestTrade {
  tradeId: string;
  gridCycleId: string;
  entryTimeUtc: number;
  entryPrice: number;
  exitTimeUtc: number | null;
  exitPrice: number | null;
  side: string;
  size: number;
  pnL: number | null;
  fees: number;
  tradeType: string;
}

export interface BacktestSummary {
  id: string;
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  totalTrades: number;
  winRate: number;
  totalPnl: number;
  maxDrawdown: number;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CoverageReport {
  symbol: string;
  intervals: IntervalCoverage[];
}

export interface IntervalCoverage {
  interval: string;
  candleCount: number;
  earliestDate: string;
  latestDate: string;
  requestedStartDate: string;
  requestedEndDate: string;
  coveragePercent: number;
  status: "full" | "partial" | "none";
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/candle.model.ts` — TypeScript interface model pattern
- `frontend/trading-ui/src/app/core/models/place-order.model.ts` — request/response model pattern
- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — backend model field names (source of truth for interface shape)
- `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — trade field names
- `src/TradingApp.Application/Backtesting/Models/EquitySnapshot.cs` — equity snapshot shape

---

### Task 2.2: Create BacktestService {#task-22-create-backtestservice}

Create the HTTP service that makes all backtest API calls through ApiRestClient.

- **Complexity**: Medium
- **Risk Factors**: Must handle query parameter encoding correctly for the list and validate endpoints
- **Files**:
  - `frontend/trading-ui/src/app/core/services/backtest.service.ts` — new file
- **Success**:
  - Service has methods: `runBacktest()`, `getBacktest()`, `validateCoverage()`, `getBacktestList()`
  - Uses `ApiRestClient` (not raw HttpClient)
  - Compiles without errors
- **Dependencies**:
  - Task 2.1 (models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/backtest.service.ts — new file
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import {
  BacktestRequest,
  BacktestResult,
  BacktestSummary,
  CoverageReport,
  PagedResult
} from "../models/backtest.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class BacktestService {
  private readonly _apiClient = inject(ApiRestClient);

  public runBacktest(request: BacktestRequest): Observable<BacktestResult> {
    return this._apiClient.post<BacktestResult>("backtests", request);
  }

  public getBacktest(id: string): Observable<BacktestResult> {
    return this._apiClient.get<BacktestResult>(`backtests/${encodeURIComponent(id)}`);
  }

  public validateCoverage(
    symbol: string,
    intervals: string[],
    startDate: string,
    endDate: string
  ): Observable<CoverageReport> {
    const params = new URLSearchParams();
    params.set("symbol", symbol);
    intervals.forEach(i => params.append("intervals", i));
    params.set("startDate", startDate);
    params.set("endDate", endDate);
    return this._apiClient.get<CoverageReport>(`backtests/validate?${params.toString()}`);
  }

  public getBacktestList(page: number = 1, pageSize: number = 20): Observable<PagedResult<BacktestSummary>> {
    return this._apiClient.get<PagedResult<BacktestSummary>>(
      `backtests?page=${page}&pageSize=${pageSize}`
    );
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — `ApiRestClient` injection pattern, query parameter construction, method naming
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — `get<T>()`, `post<T>()` method signatures

---

### Task 2.3: Add routing and navigation {#task-23-add-routing-and-navigation}

Add the `/backtesting` route to `app.routes.ts` and a "Backtesting" nav link to `app.component.html`.

- **Complexity**: Low
- **Risk Factors**: Route must be placed before the wildcard `**` redirect
- **Files**:
  - `frontend/trading-ui/src/app/app.routes.ts` — modification
  - `frontend/trading-ui/src/app/app.component.html` — modification
- **Success**:
  - `/backtesting` route exists with lazy-loaded component
  - "Backtesting" nav link appears in header
  - Clicking the link navigates to the backtesting page

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/app.routes.ts — modification
// Add before the empty path redirect:

  {
    path: "backtesting",
    loadComponent: () => import("./features/backtesting/backtest-page.component").then(m => m.BacktestPageComponent),
    title: "Backtesting"
  },
```

```html
<!-- frontend/trading-ui/src/app/app.component.html — modification -->
<!-- Add after the Order Entry link, before the closing </nav>: -->
      <a routerLink="/backtesting" routerLinkActive="app-shell__link--active" class="app-shell__link">Backtesting</a>
```

##### Pattern References

- `frontend/trading-ui/src/app/app.routes.ts` — existing `loadComponent` route entries
- `frontend/trading-ui/src/app/app.component.html` — existing `routerLink` nav link pattern

---

### Task 2.4: Create BacktestPageComponent with tab structure {#task-24-create-backtestpagecomponent-with-tab-structure}

Create the page-level component with a `mat-tab-group` containing tabs for Run, Past Results, and Compare.

- **Complexity**: Medium
- **Risk Factors**: Component must manage state shared between tabs (selected result, comparison selection)
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss` — new file
- **Success**:
  - Page renders with 3 tabs (Run, Past Results, Compare)
  - Component is standalone with Angular Material tab imports
  - Skeleton content visible in each tab
  - Component is lazy-loaded from the `/backtesting` route

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — new file
import { Component, inject } from "@angular/core";
import { MatTabsModule } from "@angular/material/tab";
import { BacktestService } from "../../core/services/backtest.service";
import { BacktestResult, BacktestSummary } from "../../core/models/backtest.model";

@Component({
  selector: "app-backtest-page",
  standalone: true,
  imports: [MatTabsModule],
  templateUrl: "./backtest-page.component.html",
  styleUrl: "./backtest-page.component.scss"
})
export class BacktestPageComponent {
  private readonly _backtestService = inject(BacktestService);

  public latestResult: BacktestResult | null = null;
  public selectedCompareIds: string[] = [];
  public prefillConfig: BacktestResult | null = null;
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html — new file -->
<div class="backtest-page">
  <h2 class="backtest-page__title">Backtesting</h2>

  <mat-tab-group class="backtest-page__tabs" animationDuration="0ms">
    <mat-tab label="Run Backtest">
      <div class="backtest-page__tab-content">
        <!-- BacktestFormComponent + BacktestResultComponent will be added in Phase 3 & 4 -->
        <p>Run backtest form placeholder</p>
      </div>
    </mat-tab>

    <mat-tab label="Past Results">
      <div class="backtest-page__tab-content">
        <!-- BacktestListComponent will be added in Phase 5 -->
        <p>Past results list placeholder</p>
      </div>
    </mat-tab>

    <mat-tab label="Compare">
      <div class="backtest-page__tab-content">
        <!-- BacktestCompareComponent will be added in Phase 5 -->
        <p>Comparison view placeholder</p>
      </div>
    </mat-tab>
  </mat-tab-group>
</div>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss — new file
.backtest-page {
  padding: 1rem;
  max-width: 1400px;
  margin: 0 auto;

  &__title {
    margin: 0 0 1rem;
    color: var(--colour-text-primary);
  }

  &__tabs {
    width: 100%;
  }

  &__tab-content {
    padding: 1rem 0;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — page-level component with `mat-tab-group`, inject() DI
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — tab structure template

---

### Task 2.5: Add unit tests for BacktestService {#task-25-add-unit-tests-for-backtestservice}

Add unit tests for the BacktestService covering all HTTP methods.

- **Complexity**: Medium
- **Risk Factors**: Must set up HttpClientTestingModule or provideHttpClientTesting
- **Files**:
  - `frontend/trading-ui/src/app/core/services/backtest.service.spec.ts` — new file
- **Success**:
  - Tests cover: runBacktest (POST), getBacktest (GET by id), validateCoverage (GET with params), getBacktestList (GET paginated)
  - All tests pass via `ng test`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/backtest.service.spec.ts — new file
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { BacktestService } from "./backtest.service";
import { BacktestRequest, BacktestResult, BacktestSummary, PagedResult } from "../models/backtest.model";
import { environment } from "../../../environments/environment";

describe("BacktestService", () => {
  let service: BacktestService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(BacktestService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it("should POST to backtests when runBacktest is called", () => {
    const request: BacktestRequest = {
      symbol: "BTC",
      intervals: ["15m"],
      startDateUtc: 1704067200000,
      endDateUtc: 1735689600000,
      initialCapital: 10000,
      feeModel: { makerFeeRate: 0.0001, takerFeeRate: 0.00035, slippageRate: 0 },
      warmupPeriod: 200,
      strategyConfigJson: "{}"
    };
    const mockResult = { totalTrades: 100 } as BacktestResult;

    service.runBacktest(request).subscribe(result => {
      expect(result.totalTrades).toBe(100);
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests`);
    expect(req.request.method).toBe("POST");
    req.flush(mockResult);
  });

  it("should GET backtest by id", () => {
    const id = "test-id-123";
    const mockResult = { totalTrades: 50 } as BacktestResult;

    service.getBacktest(id).subscribe(result => {
      expect(result.totalTrades).toBe(50);
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests/${id}`);
    expect(req.request.method).toBe("GET");
    req.flush(mockResult);
  });

  it("should GET paginated backtest list with default params", () => {
    const mockResult: PagedResult<BacktestSummary> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    };

    service.getBacktestList().subscribe(result => {
      expect(result.page).toBe(1);
      expect(result.items.length).toBe(0);
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests?page=1&pageSize=20`);
    expect(req.request.method).toBe("GET");
    req.flush(mockResult);
  });

  it("should GET validate coverage with query params", () => {
    const mockReport = { symbol: "BTC", intervals: [] };

    service.validateCoverage("BTC", ["15m", "1h"], "2024-01-01", "2024-12-31").subscribe();

    const req = httpMock.expectOne(r =>
      r.method === "GET" && r.url.includes("backtests/validate")
    );
    expect(req.request.url).toContain("symbol=BTC");
    req.flush(mockReport);
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/order.service.spec.ts` — service test pattern with TestBed, provideHttpClientTesting

---

### Task 2.6: Frontend build and lint {#task-26-frontend-build-and-lint}

Run Angular build and lint to verify all new code compiles and follows project conventions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes (if configured)
  - `npx ng test --watch=false` passes all tests
- **Dependencies**:
  - All previous tasks in Phase 2

## Phase Success Criteria

- `/backtesting` route loads the BacktestPageComponent with 3 tabs
- "Backtesting" nav link is visible in the app header
- BacktestService makes correct HTTP calls via ApiRestClient
- All TypeScript models match the expected API contract
- Frontend builds, lints, and all tests pass
