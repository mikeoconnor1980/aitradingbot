<!-- markdownlint-disable-file -->

# Task Details: Backtest Debug/Audit Log

## Phase 5: Frontend — Expandable Debug Panel

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, explicit types, SCSS BEM, `@if`/`@for` control flow, DTOs in `dtos/` folder, models in `models/` folder, enums in `enums/` folder, double quotes
- CSS variable color tokens: `--colour-profit` (green), `--colour-loss` (red), `--colour-border-subtle`, `--colour-surface-dark`, `--colour-muted`

## Design References

- Expandable row pattern: `activity-feed.component.ts` — `_expandedKeys: Set<string>`, `toggleDetails()` / `isDetailsExpanded()`, sibling `@if` row with `colspan`
- Filter pattern: `positions-table.component.ts` — `filterText: string` with `(input)` event and `.filter()` getter
- Color-coding: `activity-feed.component.scss` — `rgba(r,g,b,0.16)` background + lighter foreground
- No existing export/download functionality — built from scratch with `Blob` + `URL.createObjectURL`
- `MatTableDataSource` with `matSort` — trade-log-table currently uses this; expansion requires switching to a plain `<table>` with `@for` (matching `activity-feed` pattern) or using `multiTemplateDataRows` on `mat-table`

### Task 5.1: Add debug TypeScript models and enums {#task-51-add-debug-typescript-models-and-enums}

Create TypeScript interfaces and enums for the debug data returned by the API.

- **Complexity**: Low
- **Risk Factors**: None — model/enum definitions
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest-debug.model.ts` — new file
- **Success**:
  - Interfaces: `BacktestDebugResponse`, `CandleEvaluation`, `OrderEvent`, `GridCycleSummary`
  - Enums: `OrderEventType`, `CancellationReason`
  - Types match the API response schema
- **Dependencies**: Phase 4 (API endpoint)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest-debug.model.ts — new file

export enum OrderEventType {
  Placed = "Placed",
  Filled = "Filled",
  Cancelled = "Cancelled",
  Replaced = "Replaced"
}

export enum CancellationReason {
  GridRedeployed = "GridRedeployed",
  PositionOpened = "PositionOpened",
  StopLossTriggered = "StopLossTriggered",
  ManualCancel = "ManualCancel"
}

export interface CandleEvaluation {
  timestampUtc: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  isWarmup: boolean;
  emaFast: number;
  emaSlow: number;
  emaTrend: number;
  rsi: number;
  atr: number;
  setupDetected: boolean;
  gridLifecycleState: string;
  positionSize: number;
  positionAvgEntry: number;
  signalsEmitted: string[];
  gridCycleId: string | null;
}

export interface OrderEvent {
  timestampUtc: number;
  eventType: OrderEventType;
  orderId: string;
  side: string;
  orderType: string;
  price: number;
  size: number;
  fillPrice: number | null;
  fee: number | null;
  isMaker: boolean | null;
  cancellationReason: CancellationReason | null;
  gridCycleId: string;
}

export interface GridCycleSummary {
  gridCycleId: string;
  deployTimestampUtc: number;
  anchorPrice: number;
  levelsPlaced: number;
  levelPrices: number[];
  levelsFilled: number;
  takeProfitPrice: number;
  stopLossPrice: number;
  exitReason: string;
  cyclePnl: number;
  cycleDurationMs: number;
  closeTimestampUtc: number;
}

export interface BacktestDebugResponse {
  cycleId: string;
  candleEvaluations: CandleEvaluation[];
  orderEvents: OrderEvent[];
  gridCycleSummary: GridCycleSummary | null;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — existing model file with `export interface` pattern

---

### Task 5.2: Add getDebugData method and update BacktestTrade model {#task-52-add-getdebugdata-method-and-update-backtesttrade-model}

Add `getDebugData(id, cycleId)` method to `BacktestService`. Add `gridCycleId` and `hasAuditLog` to existing models.

- **Complexity**: Low
- **Risk Factors**: None — additive changes
- **Files**:
  - `frontend/trading-ui/src/app/core/services/backtest.service.ts` — modification
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — modification
- **Success**:
  - `BacktestService.getDebugData(id, cycleId)` returns `Observable<BacktestDebugResponse>`
  - `BacktestTrade.gridCycleId` field exists
  - `BacktestResult.hasAuditLog` field exists
- **Dependencies**: Task 5.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — modification
// Add to BacktestTrade interface:

export interface BacktestTrade {
  // ... existing fields ...
  tradeType: string;
  gridCycleId: string;  // ← add
}

// Add to BacktestResult interface:
export interface BacktestResult {
  // ... existing fields ...
  createdAt: string;
  equityTimeSeries?: EquitySnapshot[];
  hasAuditLog: boolean;  // ← add
}
```

```typescript
// frontend/trading-ui/src/app/core/services/backtest.service.ts — modification
// Add import:
import { BacktestDebugResponse } from "../models/backtest-debug.model";

// Add new method:
  public getDebugData(id: string, cycleId: string, context?: HttpContext): Observable<BacktestDebugResponse> {
    const encodedId = encodeURIComponent(id);
    const encodedCycleId = encodeURIComponent(cycleId);
    return this._apiClient.get<BacktestDebugResponse>(
      `backtests/${encodedId}/debug?cycleId=${encodedCycleId}`,
      context
    );
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/backtest.service.ts` — existing `getBacktest(id)` pattern
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — existing interface extension pattern

---

### Task 5.3: Make trade log table expandable with debug panel {#task-53-make-trade-log-table-expandable-with-debug-panel}

Transform the trade-log-table from a flat table to one with expandable rows. Add expand/collapse toggle, lazy-load debug data on expand, and handle the disabled state for runs without audit data.

- **Complexity**: High
- **Risk Factors**: Switching from `mat-table` with `MatTableDataSource` to plain `<table>` with `@for` (matching `activity-feed` pattern) to support sibling `@if` expanded rows — or using `multiTemplateDataRows` directive. The plain table approach is simpler and matches the established pattern.
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — modification
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss` — modification
- **Success**:
  - Each trade row has an expand/collapse icon button
  - Clicking expand triggers a lazy API call to `getDebugData` for that trade's `gridCycleId`
  - Expanded row shows a debug panel (details in Task 5.4)
  - Pre-existing runs (no audit data) show disabled expand button with tooltip
  - Expanding/collapsing does not lose sort state
- **Dependencies**: Task 5.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts — modification

import { Component, Input, OnChanges, SimpleChanges, inject } from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatButtonModule } from "@angular/material/button";
import { MatSortModule, MatSort } from "@angular/material/sort";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { BacktestDebugResponse } from "../../../core/models/backtest-debug.model";
import { BacktestService } from "../../../core/services/backtest.service";
import { HttpContext } from "@angular/common/http";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";

@Component({
  selector: "app-trade-log-table",
  standalone: true,
  imports: [
    DatePipe, DecimalPipe,
    MatIconModule, MatTooltipModule, MatButtonModule, MatSortModule
  ],
  templateUrl: "./trade-log-table.component.html",
  styleUrl: "./trade-log-table.component.scss"
})
export class TradeLogTableComponent implements OnChanges {
  @Input() public trades: BacktestTrade[] = [];
  @Input() public backtestId = "";
  @Input() public hasAuditLog = false;

  private readonly _backtestService = inject(BacktestService);
  private readonly _expandedCycleIds = new Set<string>();
  private readonly _debugDataCache = new Map<string, BacktestDebugResponse>();
  private readonly _loadingCycleIds = new Set<string>();

  public sortedTrades: BacktestTrade[] = [];

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["trades"]) {
      this.sortedTrades = [...this.trades];
    }
  }

  public toggleDetails(trade: BacktestTrade): void {
    if (!this.hasAuditLog) return;

    const cycleId = trade.gridCycleId;
    if (this._expandedCycleIds.has(cycleId)) {
      this._expandedCycleIds.delete(cycleId);
      return;
    }

    this._expandedCycleIds.add(cycleId);

    if (!this._debugDataCache.has(cycleId)) {
      this._loadingCycleIds.add(cycleId);
      const ctx = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
      this._backtestService.getDebugData(this.backtestId, cycleId, ctx).subscribe({
        next: (data) => {
          this._debugDataCache.set(cycleId, data);
          this._loadingCycleIds.delete(cycleId);
        },
        error: () => {
          this._loadingCycleIds.delete(cycleId);
          this._expandedCycleIds.delete(cycleId);
        }
      });
    }
  }

  public isExpanded(trade: BacktestTrade): boolean {
    return this._expandedCycleIds.has(trade.gridCycleId);
  }

  public isLoading(trade: BacktestTrade): boolean {
    return this._loadingCycleIds.has(trade.gridCycleId);
  }

  public getDebugData(trade: BacktestTrade): BacktestDebugResponse | undefined {
    return this._debugDataCache.get(trade.gridCycleId);
  }

  public getPnlClass(pnl: number | null): string {
    if (pnl == null) return "";
    return pnl >= 0 ? "trade-log__pnl--profit" : "trade-log__pnl--loss";
  }

  public getExpandTooltip(): string {
    return this.hasAuditLog ? "View debug data" : "Debug data not available for this run.";
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html — modification -->
<section class="trade-log">
  @if (sortedTrades.length === 0) {
    <div class="trade-log__empty">No completed trades recorded for this run.</div>
  } @else {
    <div class="trade-log__table-wrapper">
      <table class="trade-log__table">
        <thead>
          <tr>
            <th class="trade-log__expand-col"></th>
            <th>Entry Time</th>
            <th>Exit Time</th>
            <th>Entry Price</th>
            <th>Exit Price</th>
            <th>Side</th>
            <th>Size</th>
            <th>PnL</th>
            <th>Fees</th>
          </tr>
        </thead>
        <tbody>
          @for (trade of sortedTrades; track trade.gridCycleId + trade.entryTime) {
            <tr class="trade-log__row" (click)="toggleDetails(trade)">
              <td class="trade-log__expand-cell">
                <button mat-icon-button
                  [disabled]="!hasAuditLog"
                  [matTooltip]="getExpandTooltip()"
                  (click)="$event.stopPropagation(); toggleDetails(trade)">
                  <mat-icon>{{ isExpanded(trade) ? "expand_less" : "expand_more" }}</mat-icon>
                </button>
              </td>
              <td>{{ trade.entryTime | date: "short" }}</td>
              <td>{{ trade.exitTime ? (trade.exitTime | date: "short") : "—" }}</td>
              <td>{{ trade.entryPrice | number: "1.2-2" }}</td>
              <td>{{ trade.exitPrice !== null ? (trade.exitPrice | number: "1.2-2") : "—" }}</td>
              <td>{{ trade.side }}</td>
              <td>{{ trade.size | number: "1.4-4" }}</td>
              <td [class]="getPnlClass(trade.pnl)">
                {{ trade.pnl !== null ? "$" + (trade.pnl | number: "1.2-2") : "—" }}
              </td>
              <td>${{ trade.fees | number: "1.4-4" }}</td>
            </tr>
            @if (isExpanded(trade)) {
              <tr class="trade-log__details-row">
                <td colspan="9" class="trade-log__details-cell">
                  @if (isLoading(trade)) {
                    <div class="trade-log__loading">Loading debug data...</div>
                  } @else {
                    @let debugData = getDebugData(trade);
                    @if (debugData) {
                    <!-- Debug panel content — see Task 5.4 -->
                    <div class="trade-log__debug-panel">
                      <!-- Grid Cycle Summary, Order Events, Candle Evaluations go here -->
                      <p>Debug data loaded for cycle {{ debugData.cycleId }}</p>
                    </div>
                    }
                  }
                </td>
              </tr>
            }
          }
        </tbody>
      </table>
    </div>
  }
</section>
```

Note: This replaces the current `mat-table` with a plain `<table>` to support the sibling `@if` expanded row pattern (matching `activity-feed`). **Alternative approach**: Use `multiTemplateDataRows` directive on `mat-table` to preserve `matSort` functionality while supporting expandable rows — this is preferred if sorting is important. The implementer should choose based on whether sort functionality needs to be preserved. The `matSort` functionality is removed in the plain table approach — the implementer may keep sorting by implementing manual sort logic on `sortedTrades`, or switch to using `multiTemplateDataRows` on `mat-table` if preserving `matSort` is preferred.

```scss
// frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss — modification
// Add new styles:

  &__expand-col {
    width: 48px;
  }

  &__expand-cell {
    padding: 0 0.25rem;
  }

  &__row {
    cursor: pointer;
    &:hover {
      background: rgba(255, 255, 255, 0.03);
    }
  }

  &__details-row {
    background: rgba(15, 23, 42, 0.6);
  }

  &__details-cell {
    padding: 1rem 1.25rem;
    border-top: 1px solid var(--colour-border-subtle);
  }

  &__loading {
    color: var(--colour-muted);
    padding: 0.5rem 0;
  }

  &__debug-panel {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.ts` — `_expandedEventKeys: Set<string>`, `toggleDetails()` / `isDetailsExpanded()`
- `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.html` — sibling `@if` row pattern with `colspan`
- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — current implementation being replaced

---

### Task 5.4: Build debug panel sub-sections {#task-54-build-debug-panel-sub-sections}

Implement the three sub-sections within the expanded debug panel: grid cycle summary, order events timeline, and per-candle evaluations table.

- **Complexity**: High
- **Risk Factors**: Large amount of UI rendering; candle evaluations table could have many rows for long cycles
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — modification (replace placeholder)
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss` — modification
- **Success**:
  - Grid cycle summary shows: anchor price, levels placed, levels filled, TP/SL, exit reason, PnL, duration
  - Order events timeline shows events in chronological order with type, order details, and reason
  - Candle evaluations table shows: timestamp, OHLCV, indicators, setup detected, grid state, signals
  - All data renders correctly from the `BacktestDebugResponse`
- **Dependencies**: Task 5.3

#### Implementation Details

Replace the debug panel placeholder in the template:

```html
<!-- Inside the @if (isExpanded(trade)) block, replace placeholder with: -->
<div class="trade-log__debug-panel">
  <!-- Grid Cycle Summary -->
  @let cycle = debugData.gridCycleSummary;
  @if (cycle) {
    <div class="trade-log__section">
      <h4 class="trade-log__section-title">Grid Cycle Summary</h4>
      <div class="trade-log__summary-grid">
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Anchor Price</span>
          <span class="trade-log__value">{{ cycle.anchorPrice | number: "1.2-2" }}</span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Levels</span>
          <span class="trade-log__value">{{ cycle.levelsFilled }} / {{ cycle.levelsPlaced }} filled</span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Take Profit</span>
          <span class="trade-log__value">{{ cycle.takeProfitPrice | number: "1.2-2" }}</span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Stop Loss</span>
          <span class="trade-log__value">{{ cycle.stopLossPrice | number: "1.2-2" }}</span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Exit Reason</span>
          <span class="trade-log__value">{{ cycle.exitReason }}</span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Cycle PnL</span>
          <span class="trade-log__value" [class]="getPnlClass(cycle.cyclePnl)">
            ${{ cycle.cyclePnl | number: "1.2-2" }}
          </span>
        </div>
        <div class="trade-log__summary-item">
          <span class="trade-log__label">Duration</span>
          <span class="trade-log__value">{{ formatDuration(cycle.cycleDurationMs) }}</span>
        </div>
      </div>
    </div>
  }

  <!-- Order Events Timeline -->
  @if (debugData.orderEvents.length > 0) {
    <div class="trade-log__section">
      <h4 class="trade-log__section-title">Order Events</h4>
      <table class="trade-log__events-table">
        <thead>
          <tr>
            <th>Time</th>
            <th>Event</th>
            <th>Side</th>
            <th>Type</th>
            <th>Price</th>
            <th>Size</th>
            <th>Details</th>
          </tr>
        </thead>
        <tbody>
          @for (event of debugData.orderEvents; track event.orderId + event.eventType) {
            <tr>
              <td>{{ event.timestampUtc | date: "short" }}</td>
              <td>
                <span class="trade-log__event-badge" [class]="getEventBadgeClass(event.eventType)">
                  {{ event.eventType }}
                </span>
              </td>
              <td>{{ event.side }}</td>
              <td>{{ event.orderType }}</td>
              <td>{{ event.price | number: "1.2-2" }}</td>
              <td>{{ event.size | number: "1.4-4" }}</td>
              <td>
                @if (event.eventType === "Filled") {
                  Fill @ {{ event.fillPrice | number: "1.2-2" }}, Fee: ${{ event.fee | number: "1.4-4" }}
                } @else if (event.eventType === "Cancelled" && event.cancellationReason) {
                  {{ event.cancellationReason }}
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  }

  <!-- Candle Evaluations -->
  @if (debugData.candleEvaluations.length > 0) {
    <div class="trade-log__section">
      <h4 class="trade-log__section-title">Candle Evaluations</h4>
      <table class="trade-log__candles-table">
        <thead>
          <tr>
            <th>Time</th>
            <th>O/H/L/C</th>
            <th>EMA Fast</th>
            <th>RSI</th>
            <th>ATR</th>
            <th>Setup</th>
            <th>Grid State</th>
            <th>Signals</th>
          </tr>
        </thead>
        <tbody>
          @for (candle of getFilteredCandles(debugData); track candle.timestampUtc) {
            <tr [class.trade-log__candle-row--warmup]="candle.isWarmup">
              <td>{{ candle.timestampUtc | date: "short" }}</td>
              <td>{{ candle.open | number: "1.2-2" }} / {{ candle.high | number: "1.2-2" }} / {{ candle.low | number: "1.2-2" }} / {{ candle.close | number: "1.2-2" }}</td>
              <td>{{ candle.emaFast | number: "1.2-2" }}</td>
              <td>{{ candle.rsi | number: "1.1-1" }}</td>
              <td>{{ candle.atr | number: "1.4-4" }}</td>
              <td>{{ candle.setupDetected ? "Yes" : "No" }}</td>
              <td>{{ candle.gridLifecycleState }}</td>
              <td>{{ candle.signalsEmitted.join(", ") || "—" }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  }
</div>
```

Add helper methods to the component:

```typescript
// Add to trade-log-table.component.ts:

  public formatDuration(ms: number): string {
    const minutes = Math.floor(ms / 60000);
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return hours > 0 ? `${hours}h ${remainingMinutes}m` : `${minutes}m`;
  }

  public getEventBadgeClass(eventType: string): string {
    switch (eventType) {
      case "Filled": return "trade-log__event-badge--filled";
      case "Cancelled": return "trade-log__event-badge--cancelled";
      case "Placed": return "trade-log__event-badge--placed";
      case "Replaced": return "trade-log__event-badge--replaced";
      default: return "";
    }
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.html` — detail sub-row layout
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — `__details-grid` layout pattern
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — `mat-card` grid metric display

---

### Task 5.5: Add filtering, color-coding, and export {#task-55-add-filtering-color-coding-and-export}

Add filter controls (signal type, setup detected) within the expanded debug view. Add color-coded event badges. Add JSON/CSV export buttons.

- **Complexity**: Medium
- **Risk Factors**: CSV export for nested data (candle evaluations with arrays like `signalsEmitted`) needs flattening
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — modification
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss` — modification
- **Success**:
  - Signal type dropdown filters candle evaluations by emitted signal
  - Setup detected toggle filters by true/false
  - Event badges are color-coded: green (fills), red (cancels), blue (placements), orange (replacements)
  - JSON export downloads `{cycleId}-debug.json`
  - CSV export downloads `{cycleId}-debug.csv` with flattened candle/order data
- **Dependencies**: Task 5.3, Task 5.4

#### Implementation Details

Add filter state and methods:

```typescript
// Add to trade-log-table.component.ts:

  public signalTypeFilter = "";
  public setupDetectedFilter: boolean | null = null;

  public getFilteredCandles(debugData: BacktestDebugResponse): CandleEvaluation[] {
    let candles = debugData.candleEvaluations;

    if (this.signalTypeFilter) {
      candles = candles.filter((c) =>
        c.signalsEmitted.some((s) => s === this.signalTypeFilter)
      );
    }

    if (this.setupDetectedFilter !== null) {
      candles = candles.filter((c) => c.setupDetected === this.setupDetectedFilter);
    }

    return candles;
  }

  public getAvailableSignalTypes(debugData: BacktestDebugResponse): string[] {
    const types = new Set<string>();
    for (const candle of debugData.candleEvaluations) {
      for (const signal of candle.signalsEmitted) {
        types.add(signal);
      }
    }
    return Array.from(types);
  }

  public exportJson(debugData: BacktestDebugResponse): void {
    const json = JSON.stringify(debugData, null, 2);
    const blob = new Blob([json], { type: "application/json" });
    this.downloadBlob(blob, `${debugData.cycleId}-debug.json`);
  }

  public exportCsv(debugData: BacktestDebugResponse): void {
    const lines: string[] = [];

    // Candle evaluations CSV
    lines.push("Section: Candle Evaluations");
    lines.push("Timestamp,Open,High,Low,Close,Volume,IsWarmup,EmaFast,EmaSlow,EmaTrend,RSI,ATR,SetupDetected,GridState,PositionSize,Signals");
    for (const c of debugData.candleEvaluations) {
      lines.push([
        c.timestampUtc, c.open, c.high, c.low, c.close, c.volume,
        c.isWarmup, c.emaFast, c.emaSlow, c.emaTrend, c.rsi, c.atr,
        c.setupDetected, c.gridLifecycleState, c.positionSize,
        '"' + c.signalsEmitted.join(";") + '"'
      ].join(","));
    }

    lines.push("");
    lines.push("Section: Order Events");
    lines.push("Timestamp,EventType,OrderId,Side,OrderType,Price,Size,FillPrice,Fee,IsMaker,CancellationReason");
    for (const o of debugData.orderEvents) {
      lines.push([
        o.timestampUtc, o.eventType, o.orderId, o.side, o.orderType,
        o.price, o.size, o.fillPrice ?? "", o.fee ?? "", o.isMaker ?? "",
        o.cancellationReason ?? ""
      ].join(","));
    }

    const blob = new Blob([lines.join("\n")], { type: "text/csv" });
    this.downloadBlob(blob, `${debugData.cycleId}-debug.csv`);
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
```

Add filter controls and export buttons to the template (insert before candle evaluations table):

```html
<!-- Filter bar — add inside debug-panel, before candle evaluations section -->
<div class="trade-log__filter-bar">
  <mat-form-field class="trade-log__filter-field" appearance="outline">
    <mat-label>Signal Type</mat-label>
    <mat-select [(value)]="signalTypeFilter">
      <mat-option value="">All</mat-option>
      @for (type of getAvailableSignalTypes(debugData); track type) {
        <mat-option [value]="type">{{ type }}</mat-option>
      }
    </mat-select>
  </mat-form-field>

  <mat-form-field class="trade-log__filter-field" appearance="outline">
    <mat-label>Setup Detected</mat-label>
    <mat-select [(value)]="setupDetectedFilter">
      <mat-option [value]="null">All</mat-option>
      <mat-option [value]="true">Yes</mat-option>
      <mat-option [value]="false">No</mat-option>
    </mat-select>
  </mat-form-field>

  <div class="trade-log__export-buttons">
    <button mat-stroked-button (click)="exportJson(debugData)">
      <mat-icon>download</mat-icon> JSON
    </button>
    <button mat-stroked-button (click)="exportCsv(debugData)">
      <mat-icon>download</mat-icon> CSV
    </button>
  </div>
</div>
```

Add SCSS for color-coded badges, filters, and export:

```scss
// Add to trade-log-table.component.scss:

  &__event-badge {
    display: inline-block;
    padding: 0.125rem 0.5rem;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 600;

    &--filled {
      background: rgba(74, 222, 128, 0.16);
      color: #86efac;
    }

    &--cancelled {
      background: rgba(248, 113, 113, 0.16);
      color: #fca5a5;
    }

    &--placed {
      background: rgba(96, 165, 250, 0.16);
      color: #93c5fd;
    }

    &--replaced {
      background: rgba(251, 191, 36, 0.16);
      color: #fcd34d;
    }
  }

  &__filter-bar {
    display: flex;
    align-items: center;
    gap: 1rem;
    margin-bottom: 0.75rem;
  }

  &__filter-field {
    width: 180px;
  }

  &__export-buttons {
    margin-left: auto;
    display: flex;
    gap: 0.5rem;
  }

  &__candle-row--warmup {
    opacity: 0.5;
  }

  &__section {
    margin-bottom: 1rem;
  }

  &__section-title {
    margin: 0 0 0.5rem;
    font-size: 0.875rem;
    font-weight: 600;
    color: var(--colour-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  &__summary-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
    gap: 0.75rem;
  }

  &__summary-item {
    display: flex;
    flex-direction: column;
    gap: 0.125rem;
  }

  &__label {
    font-size: 0.75rem;
    color: var(--colour-muted);
  }

  &__value {
    font-weight: 600;
  }

  &__events-table,
  &__candles-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.8125rem;

    th, td {
      padding: 0.375rem 0.5rem;
      text-align: left;
    }

    th {
      color: var(--colour-muted);
      font-weight: 500;
      border-bottom: 1px solid var(--colour-border-subtle);
    }

    td {
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
  }
```

Note: Add `MatSelectModule`, `MatFormFieldModule`, `MatOptionModule` to the component's `imports` array in the `@Component` decorator. Update the imports list in Task 5.3's TypeScript to include these alongside the existing imports.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.scss` — badge color-coding pattern with `rgba()` backgrounds
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — `filterText` pattern
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — `MatSelectModule` usage

---

### Task 5.6: Handle disabled state and run build/lint {#task-56-handle-disabled-state-and-run-build-lint}

Ensure the parent component passes `hasAuditLog` and `backtestId` to the trade-log-table. Verify the disabled expand button and tooltip work for pre-existing runs. Run `ng build` and `npm run lint`.

- **Complexity**: Low
- **Risk Factors**: Parent component may need to extract `hasAuditLog` from the backtest result
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — modification (or wherever trade-log-table is used)
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — modification (if that's where trade-log-table is embedded)
- **Success**:
  - `app-trade-log-table` receives `[backtestId]` and `[hasAuditLog]` inputs
  - Pre-existing runs show disabled expand button with tooltip
  - `npx ng build` completes without errors
  - `npm run lint` passes (or only pre-existing issues remain)
- **Dependencies**: Tasks 5.1–5.5

#### Implementation Details

Update the parent template where `<app-trade-log-table>` is used:

```html
<!-- Update in the parent component template (likely backtest-page or backtest-result): -->
<app-trade-log-table
  [trades]="result.trades"
  [backtestId]="result.id"
  [hasAuditLog]="result.hasAuditLog">
</app-trade-log-table>
```

Run build and lint:

```bash
cd frontend/trading-ui
npx ng build
npm run lint
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — existing `<app-trade-log-table [trades]="...">` usage

## Phase Success Criteria

- TypeScript models compile and match API schema
- `BacktestService.getDebugData()` calls the correct API endpoint
- Trade log table rows are expandable/collapsible with expand icons
- Expanded rows lazy-load debug data and show grid cycle summary, order events, and candle evaluations
- Filtering by signal type and setup detected works
- Order events are color-coded (green/red/blue/orange)
- JSON and CSV export buttons download correct files
- Pre-existing runs (no audit data) show disabled expand control with tooltip
- `npx ng build` succeeds
- `npm run lint` succeeds (or no new issues)
