import { CommonModule } from "@angular/common";
import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import {
  AllCandleCoverageResponse,
  IngestionResult
} from "../../core/models/candle-management.model";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { WarnConfirmDialogComponent, WarnConfirmDialogData } from "../../core/components/warn-confirm-dialog.component";
import { CandleManagementService } from "../../core/services/candle-management.service";
import { NotificationService } from "../../core/services/notification.service";

interface IngestionState {
  isIngesting: boolean;
  result: IngestionResult | null;
  error: string | null;
}

@Component({
  selector: "app-candle-management",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./candle-management.component.html",
  styleUrl: "./candle-management.component.scss"
})
export class CandleManagementComponent implements OnInit {
  private static readonly BACKTEST_INTERVALS = ["15m", "1h", "4h"];
  private static readonly SLOW_INTERVALS = ["5m", "1d"];

  private readonly _candleService = inject(CandleManagementService);
  private readonly _notificationService = inject(NotificationService);
  private readonly _dialog = inject(MatDialog);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public coverage: AllCandleCoverageResponse | null = null;
  public isLoading = true;
  public loadError: string | null = null;
  public ingestionStates = new Map<string, IngestionState>();
  public intervalIngestionStates = new Map<string, IngestionState>();
  public intervalColumns = ["interval", "from", "to", "candleCount", "status", "actions"];

  public ngOnInit(): void {
    this._loadCoverage();
  }

  public getIngestionState(symbol: string): IngestionState {
    return this.ingestionStates.get(symbol) ?? { isIngesting: false, result: null, error: null };
  }

  public getIntervalIngestionState(symbol: string, interval: string): IngestionState {
    return this.intervalIngestionStates.get(`${symbol}/${interval}`) ?? { isIngesting: false, result: null, error: null };
  }

  public isAnyIngesting(): boolean {
    for (const state of this.ingestionStates.values()) {
      if (state.isIngesting) {
        return true;
      }
    }

    return false;
  }

  public onIngest(symbol: string): void {
    this._confirmWithWarning(
      "Ingest All Timeframes",
      `This will ingest 15m, 1h and 4h candles for ${symbol}. This may take several minutes.`,
      () => this._executeIngest(symbol)
    );
  }

  public onIngestInterval(symbol: string, interval: string): void {
    if (CandleManagementComponent.SLOW_INTERVALS.includes(interval)) {
      this._confirmWithWarning(
        "Slow Interval Warning",
        `Ingesting ${interval} candles for ${symbol} may take a very long time due to the volume of data. Are you sure?`,
        () => this._executeIngestInterval(symbol, interval)
      );
    } else {
      this._executeIngestInterval(symbol, interval);
    }
  }

  public onIngestAll(): void {
    if (!this.coverage) {
      return;
    }

    const count = this.getMissingSymbolCount();

    this._confirmWithWarning(
      "Ingest All Missing Symbols",
      `This will ingest candles for ${count} symbol(s) across multiple timeframes. This could take a very long time. Are you sure?`,
      () => {
        for (const symbol of this.coverage!.symbols) {
          const missingIntervals = symbol.intervals
            .filter((i) => i.candleCount === 0 && CandleManagementComponent.BACKTEST_INTERVALS.includes(i.interval));

          if (missingIntervals.length > 0) {
            this._executeIngest(symbol.symbol);
          }
        }
      }
    );
  }

  public onRefresh(): void {
    this._loadCoverage();
  }

  public formatDate(isoDate: string | null): string {
    if (!isoDate) {
      return "—";
    }

    return new Date(isoDate).toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric"
    });
  }

  public formatNumber(value: number): string {
    return value.toLocaleString();
  }

  public isBacktestInterval(interval: string): boolean {
    return CandleManagementComponent.BACKTEST_INTERVALS.includes(interval);
  }

  public getMissingSymbolCount(): number {
    if (!this.coverage) {
      return 0;
    }

    return this.coverage.symbols.filter((s) =>
      s.intervals.some((i) =>
        i.candleCount === 0 && CandleManagementComponent.BACKTEST_INTERVALS.includes(i.interval)
      )
    ).length;
  }

  private _loadCoverage(): void {
    this.isLoading = true;
    this.loadError = null;

    this._candleService.getCoverage().subscribe({
      next: (response: AllCandleCoverageResponse) => {
        this.coverage = response;
        this.isLoading = false;
      },
      error: (error: HttpErrorResponse) => {
        this.loadError = error.error?.message ?? error.message ?? "Failed to load coverage data";
        this.isLoading = false;
      }
    });
  }

  private _executeIngest(symbol: string): void {
    this.ingestionStates.set(symbol, { isIngesting: true, result: null, error: null });

    this._candleService.ingestBinanceCandles({
      symbol,
      intervals: CandleManagementComponent.BACKTEST_INTERVALS,
      includeMarkPrice: true
    }).subscribe({
      next: (result: IngestionResult) => {
        this.ingestionStates.set(symbol, { isIngesting: false, result, error: null });
        this._notificationService.success(
          `${symbol}: Inserted ${result.totalInserted} candles in ${(result.elapsedMs / 1000).toFixed(1)}s`
        );
        this._loadCoverage();
      },
      error: (error: HttpErrorResponse) => {
        const message = error.error?.message ?? error.message ?? "Ingestion failed";
        this.ingestionStates.set(symbol, { isIngesting: false, result: null, error: message });
        this._notificationService.error(`${symbol}: ${message}`);
      }
    });
  }

  private _executeIngestInterval(symbol: string, interval: string): void {
    const key = `${symbol}/${interval}`;
    this.intervalIngestionStates.set(key, { isIngesting: true, result: null, error: null });

    this._candleService.ingestBinanceCandles({
      symbol,
      intervals: [interval],
      includeMarkPrice: true
    }).subscribe({
      next: (result: IngestionResult) => {
        this.intervalIngestionStates.set(key, { isIngesting: false, result, error: null });
        this._notificationService.success(
          `${symbol} ${interval}: Inserted ${result.totalInserted} candles in ${(result.elapsedMs / 1000).toFixed(1)}s`
        );
        this._loadCoverage();
      },
      error: (error: HttpErrorResponse) => {
        const message = error.error?.message ?? error.message ?? "Ingestion failed";
        this.intervalIngestionStates.set(key, { isIngesting: false, result: null, error: message });
        this._notificationService.error(`${symbol} ${interval}: ${message}`);
      }
    });
  }

  private _confirmWithWarning(title: string, message: string, onConfirm: () => void): void {
    const dialogData: WarnConfirmDialogData = { title, message, confirmText: "Continue" };

    this._dialog
      .open(WarnConfirmDialogComponent, { data: dialogData, width: "440px" })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          onConfirm();
        }
      });
  }
}
