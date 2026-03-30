import { Component, DestroyRef, Input, OnChanges, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HttpContext } from "@angular/common/http";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectChange, MatSelectModule } from "@angular/material/select";
import { Subject, switchMap, tap, of, catchError, distinctUntilChanged } from "rxjs";
import { BacktestDebugResponse } from "../../../core/models/backtest-debug.model";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../../core/services/backtest.service";
import { CycleChartComponent } from "../cycle-chart/cycle-chart.component";
import { CycleNarrativeComponent } from "../cycle-narrative/cycle-narrative.component";

export interface CycleOption {
  cycleId: string;
  label: string;
  index: number;
}

@Component({
  selector: "app-grid-cycle-viewer",
  standalone: true,
  imports: [
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    CycleChartComponent,
    CycleNarrativeComponent
  ],
  templateUrl: "./grid-cycle-viewer.component.html",
  styleUrl: "./grid-cycle-viewer.component.scss"
})
export class GridCycleViewerComponent implements OnChanges {
  private readonly _backtestService = inject(BacktestService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
  private readonly _selectedCycleId$ = new Subject<string>();
  private readonly _debugCache = new Map<string, BacktestDebugResponse>();

  @Input({ required: true })
  public backtestId = "";

  @Input()
  public trades: BacktestTrade[] = [];

  @Input()
  public hasAuditLog = false;

  @Input()
  public symbol = "";

  public cycleOptions: CycleOption[] = [];
  public selectedCycleId: string | null = null;
  public debugData: BacktestDebugResponse | null = null;
  public isLoading = false;
  public errorMessage: string | null = null;

  public constructor() {
    this._selectedCycleId$.pipe(
      distinctUntilChanged(),
      tap(() => {
        this.isLoading = true;
        this.errorMessage = null;
      }),
      switchMap((cycleId: string) => {
        const cached = this._debugCache.get(cycleId);
        if (cached) {
          return of(cached);
        }

        return this._backtestService.getDebugData(this.backtestId, cycleId, this._localErrorContext).pipe(
          catchError(() => {
            this.errorMessage = "Failed to load cycle data.";
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this._destroyRef)
    ).subscribe((data: BacktestDebugResponse | null) => {
      this.isLoading = false;
      this.debugData = data;

      if (data) {
        this._debugCache.set(data.cycleId, data);
      }
    });
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["trades"]) {
      this._buildCycleOptions();
    }
  }

  public onCycleChange(event: MatSelectChange): void {
    const cycleId = event.value as string;
    this.selectedCycleId = cycleId;
    this._selectedCycleId$.next(cycleId);
  }

  private _buildCycleOptions(): void {
    const cycleTradesMap = new Map<string, BacktestTrade[]>();

    for (const trade of this.trades) {
      const cycleId = trade.gridCycleId;
      if (!cycleId) {
        continue;
      }

      if (!cycleTradesMap.has(cycleId)) {
        cycleTradesMap.set(cycleId, []);
      }

      cycleTradesMap.get(cycleId)!.push(trade);
    }

    const options: CycleOption[] = [];
    let index = 0;

    for (const [cycleId, cycleTrades] of cycleTradesMap) {
      index++;
      const timestamps = cycleTrades.map(t => t.entryTime).filter(Boolean).sort();
      const first = timestamps[0];
      const last = timestamps[timestamps.length - 1];
      const dateLabel = first
        ? this._formatShortDate(first) + (last && last !== first ? " → " + this._formatShortDate(last) : "")
        : cycleId.substring(0, 8);

      options.push({
        cycleId,
        label: `Cycle ${index} — ${dateLabel}`,
        index
      });
    }

    this.cycleOptions = options;

    if (options.length > 0 && this.hasAuditLog) {
      this.selectedCycleId = options[0].cycleId;
      this._selectedCycleId$.next(options[0].cycleId);
    }
  }

  private _formatShortDate(isoDate: string): string {
    const d = new Date(isoDate);
    return d.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
  }
}
