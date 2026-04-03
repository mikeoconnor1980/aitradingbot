import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTabGroup, MatTabsModule } from "@angular/material/tabs";
import { ActivatedRoute, Router } from "@angular/router";
import { filter, forkJoin } from "rxjs";
import { BacktestRequest, BacktestResult, CoverageReport } from "../../core/models/backtest.model";
import { GridCycleSummary } from "../../core/models/backtest-debug.model";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../core/services/backtest.service";
import { NotificationService } from "../../core/services/notification.service";
import { SignalRService } from "../../core/services/signalr.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { BacktestCompareComponent } from "./backtest-compare/backtest-compare.component";
import { BacktestResultComponent } from "./backtest-result/backtest-result.component";
import { BacktestListComponent } from "./backtest-list/backtest-list.component";
import { BacktestFormComponent, CoverageValidationRequest } from "./backtest-form/backtest-form.component";
import { CoverageReportComponent } from "./coverage-report/coverage-report.component";
import { CycleStatsTableComponent } from "./cycle-stats-table/cycle-stats-table.component";
import { EquityChartComponent } from "./equity-chart/equity-chart.component";
import { GridCycleViewerComponent } from "./grid-cycle-viewer/grid-cycle-viewer.component";
import { TradeLogTableComponent } from "./trade-log-table/trade-log-table.component";

@Component({
  selector: "app-backtest-page",
  standalone: true,
  imports: [
    MatTabsModule,
    MatButtonModule,
    MatProgressBarModule,
    BacktestFormComponent,
    BacktestListComponent,
    BacktestCompareComponent,
    CoverageReportComponent,
    CycleStatsTableComponent,
    BacktestResultComponent,
    EquityChartComponent,
    GridCycleViewerComponent,
    TradeLogTableComponent
  ],
  templateUrl: "./backtest-page.component.html",
  styleUrl: "./backtest-page.component.scss"
})
export class BacktestPageComponent implements OnInit {
  private static readonly GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

  private readonly _backtestService = inject(BacktestService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _notificationService = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @ViewChild(MatTabGroup)
  public tabGroup?: MatTabGroup;

  public latestResult: BacktestResult | null = null;
  public coverageReport: CoverageReport | null = null;
  public isRunning = false;
  public isValidating = false;
  public prefillConfig: BacktestResult | null = null;
  public compareResultA: BacktestResult | null = null;
  public compareResultB: BacktestResult | null = null;
  public apiError: string | null = null;
  public validationErrorMessage: string | null = null;
  public lastRequest: BacktestRequest | null = null;
  public cycleSummaries: GridCycleSummary[] = [];
  public selectedTabIndex = 0;
  public backtestProgress = 0;
  public backtestStatus: string | null = null;
  public pendingBacktestId: string | null = null;
  public strategyId: string | null = null;

  private _retryAction: (() => void) | null = null;
  private _lastHandledViewResultId: string | null = null;

  public ngOnInit(): void {
    this._route.queryParamMap
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((queryParamMap) => {
        this._applyStrategyIdQueryParam(queryParamMap.get("strategyId"));
        this._applyViewResultQueryParam(queryParamMap.get("viewResult"));
      });

    this._signalRService.backtestProgress$.pipe(
      takeUntilDestroyed(this._destroyRef),
      filter((p) => p.id === this.pendingBacktestId),
    ).subscribe((progress) => {
      this.backtestStatus = progress.status;
      this.backtestProgress = progress.progress;

      if (progress.status === "Completed") {
        this._loadCompletedResult(progress.id);
      } else if (progress.status === "Failed") {
        this.isRunning = false;
        this.pendingBacktestId = null;
        this.backtestStatus = null;
        this.apiError = progress.errorMessage ?? "Backtest failed.";
      }
    });
  }

  public onRunBacktest(request: BacktestRequest): void {
    this.lastRequest = request;
    this.isRunning = true;
    this.apiError = null;
    this.validationErrorMessage = null;
    this.backtestProgress = 0;
    this.backtestStatus = "Queued";
    this.cycleSummaries = [];
    this._retryAction = () => this.onRunBacktest(request);

    this._backtestService.runBacktest(request, this._localErrorContext).subscribe({
      next: (result: BacktestResult) => {
        this.pendingBacktestId = result.id;
        this.backtestStatus = result.status;
      },
      error: (error: HttpErrorResponse) => {
        this.isRunning = false;
        this.backtestStatus = null;
        this.pendingBacktestId = null;
        this._handleApiError(error, {
          notFoundMessage: "No candle data was found for that selection. Validate coverage before running again.",
          preserveRetry: error.status === 0
        });
      }
    });
  }

  public onValidateData(request: CoverageValidationRequest): void {
    this.isValidating = true;
    this.apiError = null;
    this.validationErrorMessage = null;
    this.coverageReport = null;
    this._retryAction = () => this.onValidateData(request);

    this._backtestService.validateCoverage(
      request.symbol,
      request.intervals,
      this._localErrorContext
    )
      .subscribe({
        next: (report: CoverageReport) => {
          this.coverageReport = report;
          this.isValidating = false;
          this._retryAction = null;
        },
        error: (error: HttpErrorResponse) => {
          this.isValidating = false;
          this._handleApiError(error, {
            notFoundMessage: "No candle data was found for that selection. Validate coverage before running again.",
            preserveRetry: error.status === 0
          });
        }
      });
  }

  public onRerunConfig(id: string): void {
    this.apiError = null;
    this.validationErrorMessage = null;
    this._retryAction = () => this.onRerunConfig(id);

    this._backtestService.getBacktest(id, this._localErrorContext).subscribe({
      next: (result: BacktestResult) => {
        this.prefillConfig = result;
        this.selectedTabIndex = 0;

        if (result.strategyId) {
          void this._router.navigate(["/backtesting"], {
            queryParams: { strategyId: result.strategyId }
          });
        }

        this._retryAction = null;
      },
      error: (error: HttpErrorResponse) => {
        this._handleApiError(error, {
          inlineValidation: false,
          notFoundMessage: "That saved backtest could not be found.",
          defaultMessage: "Failed to load backtest config.",
          preserveRetry: error.status === 0
        });
      }
    });
  }

  public onViewResult(id: string): void {
    this.apiError = null;
    this.validationErrorMessage = null;
    this._retryAction = () => this.onViewResult(id);

    this._backtestService.getBacktest(id, this._localErrorContext).subscribe({
      next: (result: BacktestResult) => {
        this.latestResult = result;
        this.prefillConfig = result;
        this.selectedTabIndex = 0;
        this._retryAction = null;
      },
      error: (error: HttpErrorResponse) => {
        this._handleApiError(error, {
          inlineValidation: false,
          notFoundMessage: "That saved backtest could not be found.",
          defaultMessage: "Failed to load backtest result.",
          preserveRetry: error.status === 0
        });
      }
    });
  }

  public onCompareSelected(ids: string[]): void {
    if (ids.length !== 2) {
      return;
    }

    this.apiError = null;
    this.validationErrorMessage = null;
    this._retryAction = () => this.onCompareSelected(ids);

    forkJoin([
      this._backtestService.getBacktest(ids[0], this._localErrorContext),
      this._backtestService.getBacktest(ids[1], this._localErrorContext)
    ]).subscribe({
      next: ([resultA, resultB]) => {
        this.compareResultA = resultA;
        this.compareResultB = resultB;
        this.selectedTabIndex = 2;
        this._retryAction = null;
      },
      error: (error: HttpErrorResponse) => {
        this._handleApiError(error, {
          inlineValidation: false,
          notFoundMessage: "One of the selected backtests could not be found.",
          defaultMessage: "Failed to load comparison data.",
          preserveRetry: error.status === 0
        });
      }
    });
  }

  public dismissApiError(): void {
    this.apiError = null;
    this._retryAction = null;
  }

  public onCycleSummariesLoaded(summaries: GridCycleSummary[]): void {
    this.cycleSummaries = summaries;
  }

  public onRetry(): void {
    this._retryAction?.();
  }

  public get showRetry(): boolean {
    return this.apiError !== null && this._retryAction !== null;
  }

  private _loadCompletedResult(id: string): void {
    this._backtestService.getBacktest(id, this._localErrorContext).subscribe({
      next: (result: BacktestResult) => {
        this.latestResult = result;
        this.prefillConfig = result;
        this.isRunning = false;
        this.pendingBacktestId = null;
        this.backtestStatus = null;
        this.selectedTabIndex = 0;
        this._retryAction = null;
      },
      error: () => {
        this.isRunning = false;
        this.pendingBacktestId = null;
        this.backtestStatus = null;
        this.apiError = "Backtest completed but failed to load results. Check Past Results tab.";
      }
    });
  }

  private _handleApiError(
    error: HttpErrorResponse,
    options?: {
      inlineValidation?: boolean;
      notFoundMessage?: string;
      timeoutMessage?: string;
      defaultMessage?: string;
      preserveRetry?: boolean;
    }
  ): void {
    const message = formatErrorPayload(error);
    const inlineValidation = options?.inlineValidation ?? true;

    if (!options?.preserveRetry) {
      this._retryAction = null;
    }

    if (error.status === 400 && inlineValidation) {
      this.validationErrorMessage = message;
      this.apiError = null;
      return;
    }

    if (error.status === 408) {
      this.apiError = options?.timeoutMessage ?? "Backtest timed out. Try a shorter date range.";
      return;
    }

    if (error.status === 404) {
      this.apiError = options?.notFoundMessage ?? "No candle data was found for that selection. Validate coverage before running again.";
      return;
    }

    if (error.status === 0) {
      this.apiError = "Unable to reach API. Check your connection and try again.";
      return;
    }

    this.apiError = options?.defaultMessage ?? message;
  }

  private _applyStrategyIdQueryParam(strategyId: string | null): void {
    if (strategyId === null || strategyId.trim().length === 0) {
      this.strategyId = null;
      return;
    }

    if (!BacktestPageComponent.GUID_PATTERN.test(strategyId)) {
      this.strategyId = null;
      this._notificationService.error("Strategy not found. Please select a different strategy.");
      return;
    }

    this.strategyId = strategyId;
  }

  private _applyViewResultQueryParam(viewResultId: string | null): void {
    if (viewResultId === null || viewResultId.trim().length === 0) {
      this._lastHandledViewResultId = null;
      return;
    }

    if (!BacktestPageComponent.GUID_PATTERN.test(viewResultId)) {
      return;
    }

    if (this._lastHandledViewResultId === viewResultId) {
      return;
    }

    this._lastHandledViewResultId = viewResultId;
    this.onViewResult(viewResultId);
  }
}