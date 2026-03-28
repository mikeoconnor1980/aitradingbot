import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, OnDestroy, OnInit, ViewChild, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTabGroup, MatTabsModule } from "@angular/material/tabs";
import { Subject, filter, forkJoin, takeUntil } from "rxjs";
import { BacktestRequest, BacktestResult, CoverageReport } from "../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../core/services/backtest.service";
import { SignalRService } from "../../core/services/signalr.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { BacktestCompareComponent } from "./backtest-compare/backtest-compare.component";
import { BacktestResultComponent } from "./backtest-result/backtest-result.component";
import { BacktestListComponent } from "./backtest-list/backtest-list.component";
import { BacktestFormComponent, CoverageValidationRequest } from "./backtest-form/backtest-form.component";
import { CoverageReportComponent } from "./coverage-report/coverage-report.component";
import { EquityChartComponent } from "./equity-chart/equity-chart.component";
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
    BacktestResultComponent,
    EquityChartComponent,
    TradeLogTableComponent
  ],
  templateUrl: "./backtest-page.component.html",
  styleUrl: "./backtest-page.component.scss"
})
export class BacktestPageComponent implements OnInit, OnDestroy {
  private readonly _backtestService = inject(BacktestService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
  private readonly _destroy$ = new Subject<void>();

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
  public selectedTabIndex = 0;
  public backtestProgress = 0;
  public backtestStatus: string | null = null;
  public pendingBacktestId: string | null = null;

  private _retryAction: (() => void) | null = null;

  public ngOnInit(): void {
    this._signalRService.backtestProgress$.pipe(
      filter((p) => p.id === this.pendingBacktestId),
      takeUntil(this._destroy$)
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

  public ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  public onRunBacktest(request: BacktestRequest): void {
    this.lastRequest = request;
    this.isRunning = true;
    this.apiError = null;
    this.validationErrorMessage = null;
    this.backtestProgress = 0;
    this.backtestStatus = "Queued";
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
      request.startDate,
      request.endDate,
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
}