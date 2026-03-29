import { DecimalPipe } from "@angular/common";
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HttpContext } from "@angular/common/http";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSortModule, Sort } from "@angular/material/sort";
import { MatTableModule } from "@angular/material/table";
import { forkJoin, of, catchError } from "rxjs";
import { BacktestDebugResponse, GridCycleSummary } from "../../../core/models/backtest-debug.model";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../../core/services/backtest.service";

type SortableColumn = "index" | "duration" | "levelsFilled" | "cyclePnl" | "exitReason" | "anchorPrice";

export interface CycleStatsRow {
  index: number;
  cycleId: string;
  anchorPrice: number;
  levelsPlaced: number;
  levelsFilled: number;
  cyclePnl: number;
  exitReason: string;
  duration: string;
  durationMs: number;
  totalFees: number;
  tradeCount: number;
}

@Component({
  selector: "app-cycle-stats-table",
  standalone: true,
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSortModule,
    MatTableModule
  ],
  templateUrl: "./cycle-stats-table.component.html",
  styleUrl: "./cycle-stats-table.component.scss"
})
export class CycleStatsTableComponent implements OnChanges {
  private readonly _backtestService = inject(BacktestService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @Input({ required: true })
  public backtestId = "";

  @Input()
  public trades: BacktestTrade[] = [];

  @Input()
  public hasAuditLog = false;

  @Output()
  public summariesLoaded = new EventEmitter<GridCycleSummary[]>();

  public rows: CycleStatsRow[] = [];
  public sortedRows: CycleStatsRow[] = [];
  public isLoading = false;
  public errorMessage: string | null = null;
  public sortColumn: SortableColumn | null = null;
  public sortDirection: "asc" | "desc" | "" = "";

  public readonly displayedColumns = [
    "index", "anchorPrice", "levelsFilled", "cyclePnl", "totalFees", "exitReason", "duration"
  ];

  public get totalCyclePnl(): number {
    return this.rows.reduce((sum: number, row: CycleStatsRow) => sum + row.cyclePnl, 0);
  }

  public get totalCycleFees(): number {
    return this.rows.reduce((sum: number, row: CycleStatsRow) => sum + row.totalFees, 0);
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["trades"] || changes["backtestId"]) {
      this._loadCycleStats();
    }
  }

  public onSortChange(sort: Sort): void {
    this.sortColumn = sort.active as SortableColumn;
    this.sortDirection = sort.direction;
    this._applySorting();
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "cycle-stats__pnl--profit" : "cycle-stats__pnl--loss";
  }

  public getExitReasonClass(reason: string): string {
    switch (reason) {
      case "TakeProfit":
        return "cycle-stats__exit--tp";
      case "StopLoss":
        return "cycle-stats__exit--sl";
      case "Breakdown":
        return "cycle-stats__exit--breakdown";
      default:
        return "";
    }
  }

  public getExitReasonLabel(reason: string): string {
    switch (reason) {
      case "TakeProfit":
        return "Take Profit";
      case "StopLoss":
        return "Stop Loss";
      case "Breakdown":
        return "Breakdown";
      default:
        return reason;
    }
  }

  private _loadCycleStats(): void {
    const cycleIds = this._getUniqueCycleIds();

    if (cycleIds.length === 0 || !this.hasAuditLog) {
      this._buildRowsFromTradesOnly();
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const requests = cycleIds.map((cycleId: string) =>
      this._backtestService.getDebugData(this.backtestId, cycleId, this._localErrorContext).pipe(
        catchError(() => of(null))
      )
    );

    forkJoin(requests)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((results: (BacktestDebugResponse | null)[]) => {
        this.isLoading = false;
        const summaries: GridCycleSummary[] = [];

        const rows: CycleStatsRow[] = [];
        for (let i = 0; i < cycleIds.length; i++) {
          const cycleId = cycleIds[i];
          const debug = results[i];
          const cycleTrades = this.trades.filter((t: BacktestTrade) => t.gridCycleId === cycleId);

          if (debug?.gridCycleSummary) {
            const summary = debug.gridCycleSummary;
            summaries.push(summary);
            rows.push({
              index: i + 1,
              cycleId,
              anchorPrice: summary.anchorPrice,
              levelsPlaced: summary.levelsPlaced,
              levelsFilled: summary.levelsFilled,
              cyclePnl: summary.cyclePnl,
              exitReason: summary.exitReason,
              duration: this._formatDuration(summary.cycleDurationMs),
              durationMs: summary.cycleDurationMs,
              totalFees: cycleTrades.reduce((sum: number, t: BacktestTrade) => sum + t.fees, 0),
              tradeCount: cycleTrades.length
            });
          } else {
            rows.push(this._buildRowFromTrades(i + 1, cycleId, cycleTrades));
          }
        }

        this.rows = rows;
        this._applySorting();
        this.summariesLoaded.emit(summaries);
      });
  }

  private _buildRowsFromTradesOnly(): void {
    const cycleIds = this._getUniqueCycleIds();
    const rows: CycleStatsRow[] = [];

    for (let i = 0; i < cycleIds.length; i++) {
      const cycleId = cycleIds[i];
      const cycleTrades = this.trades.filter((t: BacktestTrade) => t.gridCycleId === cycleId);
      rows.push(this._buildRowFromTrades(i + 1, cycleId, cycleTrades));
    }

    this.rows = rows;
    this._applySorting();
  }

  private _buildRowFromTrades(index: number, cycleId: string, cycleTrades: BacktestTrade[]): CycleStatsRow {
    const totalPnl = cycleTrades.reduce((sum: number, t: BacktestTrade) => sum + (t.pnl ?? 0), 0);
    const totalFees = cycleTrades.reduce((sum: number, t: BacktestTrade) => sum + t.fees, 0);

    let durationMs = 0;
    if (cycleTrades.length > 0) {
      const entries = cycleTrades.map((t: BacktestTrade) => new Date(t.entryTime).getTime());
      const exits = cycleTrades
        .filter((t: BacktestTrade) => t.exitTime !== null)
        .map((t: BacktestTrade) => new Date(t.exitTime!).getTime());

      const earliest = Math.min(...entries);
      const latest = exits.length > 0 ? Math.max(...exits) : Math.max(...entries);
      durationMs = latest - earliest;
    }

    return {
      index,
      cycleId,
      anchorPrice: cycleTrades.length > 0 ? cycleTrades[0].entryPrice : 0,
      levelsPlaced: cycleTrades.length,
      levelsFilled: cycleTrades.filter((t: BacktestTrade) => t.exitTime !== null).length,
      cyclePnl: totalPnl,
      exitReason: totalPnl > 0 ? "TakeProfit" : "Unknown",
      duration: this._formatDuration(durationMs),
      durationMs,
      totalFees,
      tradeCount: cycleTrades.length
    };
  }

  private _getUniqueCycleIds(): string[] {
    const seen = new Set<string>();
    const ids: string[] = [];

    for (const trade of this.trades) {
      const cycleId = trade.gridCycleId;
      if (cycleId && !seen.has(cycleId)) {
        seen.add(cycleId);
        ids.push(cycleId);
      }
    }

    return ids;
  }

  private _applySorting(): void {
    if (!this.sortColumn || !this.sortDirection) {
      this.sortedRows = [...this.rows];
      return;
    }

    const column = this.sortColumn;
    const direction = this.sortDirection === "asc" ? 1 : -1;

    this.sortedRows = [...this.rows].sort((a: CycleStatsRow, b: CycleStatsRow) => {
      let comparison = 0;

      switch (column) {
        case "index":
          comparison = a.index - b.index;
          break;
        case "anchorPrice":
          comparison = a.anchorPrice - b.anchorPrice;
          break;
        case "levelsFilled":
          comparison = a.levelsFilled - b.levelsFilled;
          break;
        case "cyclePnl":
          comparison = a.cyclePnl - b.cyclePnl;
          break;
        case "exitReason":
          comparison = a.exitReason.localeCompare(b.exitReason);
          break;
        case "duration":
          comparison = a.durationMs - b.durationMs;
          break;
      }

      return comparison * direction;
    });
  }

  private _formatDuration(totalMs: number): string {
    const totalMinutes = Math.floor(totalMs / 60000);

    if (totalMinutes < 60) {
      return `${totalMinutes}m`;
    }

    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours < 24) {
      return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
    }

    const days = Math.floor(hours / 24);
    const remainingHours = hours % 24;
    const parts = [`${days}d`];
    if (remainingHours > 0) {
      parts.push(`${remainingHours}h`);
    }
    return parts.join(" ");
  }
}
