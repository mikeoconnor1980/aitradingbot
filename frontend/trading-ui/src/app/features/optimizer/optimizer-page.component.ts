import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTabGroup, MatTabsModule } from "@angular/material/tabs";
import { Router } from "@angular/router";
import { filter } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { OptimizationResult, OptimizationRun, RunOptimizationRequest, parseOptimizationStrategyConfig } from "../../core/models/optimizer.model";
import { OptimizerService } from "../../core/services/optimizer.service";
import { SignalRService } from "../../core/services/signalr.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { OptimizerConfigFormComponent } from "./optimizer-config-form/optimizer-config-form.component";
import { OptimizerDetailComponent } from "./optimizer-detail/optimizer-detail.component";
import { OptimizerHistoryListComponent } from "./optimizer-history-list/optimizer-history-list.component";
import { OptimizerResultsTableComponent } from "./optimizer-results-table/optimizer-results-table.component";

@Component({
  selector: "app-optimizer-page",
  standalone: true,
  imports: [
    MatButtonModule,
    MatProgressBarModule,
    MatTabsModule,
    OptimizerConfigFormComponent,
    OptimizerDetailComponent,
    OptimizerHistoryListComponent,
    OptimizerResultsTableComponent
  ],
  templateUrl: "./optimizer-page.component.html",
  styleUrl: "./optimizer-page.component.scss"
})
export class OptimizerPageComponent implements OnInit {
  private readonly _optimizerService = inject(OptimizerService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @ViewChild(MatTabGroup)
  public tabGroup?: MatTabGroup;

  public latestRun: OptimizationRun | null = null;
  public selectedResult: OptimizationResult | null = null;
  public selectedTabIndex = 0;
  public isRunning = false;
  public apiError: string | null = null;
  public pendingOptimizationId: string | null = null;
  public optimizationStatus: string | null = null;
  public optimizationCompleted = 0;
  public optimizationTotal = 0;
  public historyRefreshToken = 0;

  public ngOnInit(): void {
    this._signalRService.optimizationProgress$.pipe(
      takeUntilDestroyed(this._destroyRef),
      filter((progress) => progress.id === this.pendingOptimizationId)
    ).subscribe((progress) => {
      this.optimizationStatus = progress.status;
      this.optimizationCompleted = progress.completed;
      this.optimizationTotal = progress.total;

      if (progress.status === "Completed") {
        this._loadOptimization(progress.id, true);
        return;
      }

      if (progress.status === "Failed") {
        this.isRunning = false;
        this.pendingOptimizationId = null;
        this.apiError = progress.errorMessage ?? "Optimization failed.";
      }
    });
  }

  public onRunOptimization(request: RunOptimizationRequest): void {
    this.apiError = null;
    this.isRunning = true;
    this.optimizationStatus = "Queued";
    this.optimizationCompleted = 0;
    this.optimizationTotal = 0;
    this.latestRun = null;
    this.selectedResult = null;

    this._optimizerService.runOptimization(request, this._localErrorContext).subscribe({
      next: (run) => {
        this.pendingOptimizationId = run.id;
        this.optimizationStatus = run.status;
        this.optimizationCompleted = run.completedCount;
        this.optimizationTotal = run.totalCombinations;
        this.historyRefreshToken += 1;
      },
      error: (error: HttpErrorResponse) => {
        this.isRunning = false;
        this.optimizationStatus = null;
        this.pendingOptimizationId = null;
        this.apiError = error.status === 0
          ? "Unable to reach API. Check your connection and try again."
          : formatErrorPayload(error);
      }
    });
  }

  public onOpenHistoryResult(id: string): void {
    this._loadOptimization(id, true);
  }

  public onSelectResult(result: OptimizationResult): void {
    this.selectedResult = result;
  }

  public onCreateStrategy(result: OptimizationResult): void {
    const strategyConfig = parseOptimizationStrategyConfig(result.strategyConfigJson);

    if (strategyConfig === null) {
      this.apiError = "The saved strategy configuration could not be parsed.";
      return;
    }

    const promotedConfig = {
      ...strategyConfig,
      strategyName: `${result.signalDescription} Strategy`,
      source: {
        entryPoint: "optimizer",
        summary: `Promoted from optimizer run ${this.latestRun?.id ?? ""}`,
        sourceText: result.signalDescription,
      }
    };

    void this._router.navigate(["/strategies/new"], {
      state: {
        prefillConfig: promotedConfig
      }
    });
  }

  public dismissApiError(): void {
    this.apiError = null;
  }

  public get progressPercent(): number {
    if (this.optimizationTotal <= 0) {
      return 0;
    }

    return Math.round((this.optimizationCompleted / this.optimizationTotal) * 100);
  }

  private _loadOptimization(id: string, showResultsTab: boolean): void {
    this.apiError = null;

    this._optimizerService.getOptimization(id, this._localErrorContext).subscribe({
      next: (run) => {
        this.latestRun = run;
        this.selectedResult = run.results[0] ?? null;
        this.isRunning = run.status === "Queued" || run.status === "Running";
        this.pendingOptimizationId = this.isRunning ? run.id : null;
        this.optimizationStatus = run.status;
        this.optimizationCompleted = run.completedCount;
        this.optimizationTotal = run.totalCombinations;
        this.historyRefreshToken += 1;

        if (showResultsTab) {
          this.selectedTabIndex = 1;
        }
      },
      error: (error: HttpErrorResponse) => {
        this.apiError = error.status === 0
          ? "Unable to reach API. Check your connection and try again."
          : formatErrorPayload(error);
      }
    });
  }
}