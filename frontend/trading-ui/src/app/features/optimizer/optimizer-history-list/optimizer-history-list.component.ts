import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { DatePipe, DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { OptimizationListResult, OptimizationRunSummary } from "../../../core/models/optimizer.model";
import { OptimizerService } from "../../../core/services/optimizer.service";
import { formatErrorPayload } from "../../../core/utils/error-utils";

@Component({
  selector: "app-optimizer-history-list",
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatPaginatorModule, MatProgressBarModule, MatProgressSpinnerModule, MatTableModule],
  templateUrl: "./optimizer-history-list.component.html",
  styleUrl: "./optimizer-history-list.component.scss"
})
export class OptimizerHistoryListComponent implements OnInit, OnChanges {
  private readonly _optimizerService = inject(OptimizerService);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @Input()
  public refreshToken = 0;

  @Input()
  public selectedId: string | null = null;

  @Output()
  public openResult = new EventEmitter<string>();

  @Output()
  public reuseConfig = new EventEmitter<string>();

  @Output()
  public cancelRun = new EventEmitter<string>();

  public readonly displayedColumns = ["createdAt", "symbol", "status", "progress", "qualifiedCount", "topFitnessScore", "topTotalPnl", "actions"];
  public results: OptimizationRunSummary[] = [];
  public totalCount = 0;
  public page = 1;
  public pageSize = 10;
  public isLoading = false;
  public errorMessage: string | null = null;

  public ngOnInit(): void {
    this.loadPage();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["refreshToken"] && !changes["refreshToken"].firstChange) {
      this.page = 1;
      this.loadPage();
    }
  }

  public loadPage(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this._optimizerService.getOptimizationList(this.page, this.pageSize, this._localErrorContext).subscribe({
      next: (result: OptimizationListResult) => {
        this.results = result.items;
        this.totalCount = result.totalCount;
        this.page = result.page;
        this.pageSize = result.pageSize;
        this.isLoading = false;
      },
      error: (error: HttpErrorResponse) => {
        this.results = [];
        this.totalCount = 0;
        this.isLoading = false;
        this.errorMessage = error.status === 0
          ? "Unable to reach API. Check your connection and try again."
          : formatErrorPayload(error);
      }
    });
  }

  public onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadPage();
  }

  public onOpenResult(id: string): void {
    this.openResult.emit(id);
  }

  public onReuseConfig(id: string): void {
    this.reuseConfig.emit(id);
  }

  public onCancelRun(id: string): void {
    this.cancelRun.emit(id);
  }

  public getProgressValue(run: OptimizationRunSummary): number {
    if (run.totalCombinations <= 0) {
      return 0;
    }

    return Math.round((run.completedCount / run.totalCombinations) * 100);
  }
}