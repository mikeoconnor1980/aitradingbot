import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { DatePipe, DecimalPipe } from "@angular/common";
import { Component, DestroyRef, EventEmitter, OnInit, Output, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { Subject, debounceTime, distinctUntilChanged, map } from "rxjs";
import { BacktestSummary } from "../../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../../core/services/backtest.service";
import { formatErrorPayload } from "../../../core/utils/error-utils";

@Component({
  selector: "app-backtest-list",
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./backtest-list.component.html",
  styleUrl: "./backtest-list.component.scss"
})
export class BacktestListComponent implements OnInit {
  private readonly _backtestService = inject(BacktestService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
  private readonly _filterChanged$ = new Subject<void>();

  @Output()
  public viewResult = new EventEmitter<string>();

  @Output()
  public rerunConfig = new EventEmitter<string>();

  @Output()
  public compareSelected = new EventEmitter<string[]>();

  public results: BacktestSummary[] = [];
  public totalCount = 0;
  public page = 1;
  public pageSize = 20;
  public symbolFilter = "";
  public strategyFilter = "";
  public readonly selectedIds = new Set<string>();
  public isLoading = false;
  public errorMessage: string | null = null;

  public readonly displayedColumns = [
    "select",
    "createdAt",
    "symbol",
    "strategyName",
    "dateRange",
    "intervals",
    "totalTrades",
    "winRate",
    "totalPnl",
    "maxDrawdown",
    "profitFactor",
    "sqn",
    "actions"
  ];

  public ngOnInit(): void {
    this._filterChanged$.pipe(
      debounceTime(350),
      map(() => `${this.symbolFilter.trim()}|${this.strategyFilter.trim()}`),
      distinctUntilChanged(),
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => {
      this.page = 1;
      this.loadPage();
    });

    this.loadPage();
  }

  public onFilterChange(): void {
    this._filterChanged$.next();
  }

  public clearFilters(): void {
    this.symbolFilter = "";
    this.strategyFilter = "";
    this.page = 1;
    this.loadPage();
  }

  public get hasActiveFilters(): boolean {
    return this.symbolFilter.trim().length > 0 || this.strategyFilter.trim().length > 0;
  }

  public loadPage(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this._backtestService.getBacktestList(
      this.page,
      this.pageSize,
      this.symbolFilter.trim() || undefined,
      this.strategyFilter.trim() || undefined,
      this._localErrorContext
    )
      .subscribe({
        next: (result) => {
          this.results = result.items;
          this.totalCount = result.totalCount;
          this.page = result.page;
          this.pageSize = result.pageSize;
          this._pruneSelection();
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

  public toggleSelection(id: string): void {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
      return;
    }

    if (this.selectedIds.size >= 2) {
      return;
    }

    this.selectedIds.add(id);
  }

  public isSelected(id: string): boolean {
    return this.selectedIds.has(id);
  }

  public canSelect(id: string): boolean {
    return this.selectedIds.has(id) || this.selectedIds.size < 2;
  }

  public onCompare(): void {
    this.compareSelected.emit([...this.selectedIds]);
  }

  public onViewResult(id: string): void {
    this.viewResult.emit(id);
  }

  public onNavigateToStrategy(strategyId: string): void {
    void this._router.navigate(["/strategies", strategyId, "edit"]);
  }

  public onRerun(id: string, event: Event): void {
    event.stopPropagation();
    this.rerunConfig.emit(id);
  }

  public isDeletedStrategy(strategyName: string | null | undefined): boolean {
    return strategyName?.endsWith(" (deleted)") ?? false;
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "backtest-list__pnl--profit" : "backtest-list__pnl--loss";
  }

  public hasInfiniteProfitFactor(summary: BacktestSummary): boolean {
    return (summary.profitFactor === null || summary.profitFactor === undefined)
      && summary.totalTrades > 0
      && summary.winRate >= 100;
  }

  private _pruneSelection(): void {
    const currentIds = new Set(this.results.map((result) => result.id));

    for (const selectedId of [...this.selectedIds]) {
      if (!currentIds.has(selectedId)) {
        this.selectedIds.delete(selectedId);
      }
    }
  }
}