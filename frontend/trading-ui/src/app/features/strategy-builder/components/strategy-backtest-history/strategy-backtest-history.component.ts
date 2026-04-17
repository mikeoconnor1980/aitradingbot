import { DatePipe, DecimalPipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, Input, OnChanges, SimpleChanges, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { BacktestSummary } from "../../../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../../core/interceptors/http-context-tokens";
import { NotificationFacade } from "../../../../core/services/notification-facade.service";
import { BacktestService } from "../../../../core/services/backtest.service";

interface RevisionGroup {
  revisionNumber: number | null;
  backtests: BacktestSummary[];
}

@Component({
  selector: "app-strategy-backtest-history",
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule, MatTableModule, MatTooltipModule],
  templateUrl: "./strategy-backtest-history.component.html",
  styleUrl: "./strategy-backtest-history.component.scss"
})
export class StrategyBacktestHistoryComponent implements OnChanges {
  private readonly _backtestService = inject(BacktestService);
  private readonly _router = inject(Router);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @Input({ required: true })
  public strategyId!: string;

  public readonly displayedColumns = ["createdAt", "totalPnl", "winRate", "maxDrawdown", "actions"];
  public revisionGroups: RevisionGroup[] = [];
  public isLoading = false;
  public isEmpty = false;

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["strategyId"]?.currentValue) {
      this._loadHistory();
    }
  }

  public onViewBacktest(backtestId: string): void {
    void this._router.navigate(["/backtesting"], {
      queryParams: {
        strategyId: this.strategyId,
        viewResult: backtestId
      }
    });
  }

  public getPnlClass(totalPnl: number): string {
    return totalPnl >= 0
      ? "strategy-backtest-history__value--profit"
      : "strategy-backtest-history__value--loss";
  }

  private _loadHistory(): void {
    this.isLoading = true;
    this.isEmpty = false;

    this._backtestService.getBacktestsByStrategy(this.strategyId, 1, 50, this._localErrorContext).subscribe({
      next: (result) => {
        this.revisionGroups = this._groupByRevision(result.items);
        this.isEmpty = result.items.length === 0;
        this.isLoading = false;
      },
      error: () => {
        this.revisionGroups = [];
        this.isEmpty = true;
        this.isLoading = false;
        this._notifications.error("Failed to load backtest history.");
      }
    });
  }

  private _groupByRevision(backtests: BacktestSummary[]): RevisionGroup[] {
    const groups = new Map<number | null, BacktestSummary[]>();

    for (const backtest of backtests) {
      const revisionNumber = backtest.strategyRevisionId ?? null;
      const group = groups.get(revisionNumber) ?? [];
      group.push(backtest);
      groups.set(revisionNumber, group);
    }

    return Array.from(groups.entries())
      .map(([revisionNumber, items]) => ({
        revisionNumber,
        backtests: items.sort((left, right) => right.createdAt.localeCompare(left.createdAt))
      }))
      .sort((left, right) => (right.revisionNumber ?? 0) - (left.revisionNumber ?? 0));
  }
}