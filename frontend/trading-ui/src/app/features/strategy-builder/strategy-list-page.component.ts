import { DatePipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, OnInit, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { NotificationService } from "../../core/services/notification.service";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { StrategySummaryDto } from "./models/strategy.model";
import { StrategyApiService } from "./services/strategy-api.service";

@Component({
  selector: "app-strategy-list-page",
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule, MatTableModule, MatTooltipModule],
  templateUrl: "./strategy-list-page.component.html",
  styleUrl: "./strategy-list-page.component.scss"
})
export class StrategyListPageComponent implements OnInit {
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _notifications = inject(NotificationService);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public readonly displayedColumns = ["name", "market", "timeframe", "direction", "strategyMode", "createdAt", "updatedAt", "actions"];
  public strategies: StrategySummaryDto[] = [];
  public isLoading = true;

  public ngOnInit(): void {
    this._loadStrategies();
  }

  public onNewStrategy(): void {
    void this._router.navigate(["/strategies/new"]);
  }

  public onEdit(strategy: StrategySummaryDto): void {
    void this._router.navigate(["/strategies", strategy.id, "edit"]);
  }

  public onBacktestStrategy(strategyId: string): void {
    void this._router.navigate(["/backtesting"], {
      queryParams: { strategyId }
    });
  }

  public onDelete(strategy: StrategySummaryDto): void {
    const dialogData: ConfirmDialogData = {
      title: "Delete Strategy",
      message: `Are you sure you want to delete '${strategy.name}'?`,
      confirmText: "Delete",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "400px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this._strategyApi.deleteStrategy(strategy.id, this._localErrorContext).subscribe({
        next: () => {
          this.strategies = this.strategies.filter((item) => item.id !== strategy.id);
          this._notifications.success(`Strategy '${strategy.name}' deleted`);
        },
        error: () => {
          this._notifications.error("Failed to delete strategy.");
        }
      });
    });
  }

  private _loadStrategies(): void {
    this.isLoading = true;

    this._strategyApi.getStrategies(this._localErrorContext).subscribe({
      next: (strategies) => {
        this.strategies = strategies;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this._notifications.error("Failed to load strategies.");
      }
    });
  }
}