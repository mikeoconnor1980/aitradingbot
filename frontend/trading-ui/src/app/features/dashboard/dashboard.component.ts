import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTabsModule } from "@angular/material/tabs";
import { Observable, Subject, forkJoin, interval, of, timer } from "rxjs";
import { catchError, startWith, switchMap, tap } from "rxjs/operators";
import { AccountSummary } from "../../core/models/account-summary.model";
import { OpenOrder } from "../../core/models/open-order.model";
import { Position } from "../../core/models/position.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { AccountSummaryComponent } from "./account-summary/account-summary.component";
import { OrdersTableComponent } from "./orders-table/orders-table.component";
import { PositionsTableComponent } from "./positions-table/positions-table.component";

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    AccountSummaryComponent,
    PositionsTableComponent,
    OrdersTableComponent
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss"
})
export class DashboardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _refresh$ = new Subject<void>();
  private _consecutiveErrors = 0;

  public accountSummary: AccountSummary | null = null;
  public positions: Position[] = [];
  public orders: OpenOrder[] = [];
  public isLoading = true;
  public isStale = false;
  public showErrorBanner = false;
  public errorMessage = "";
  public lastUpdated: Date | null = null;
  public secondsAgo = 0;

  public ngOnInit(): void {
    this._startPolling();
    this._startStalenessTimer();
  }

  public onManualRefresh(): void {
    this._refresh$.next();
  }

  private _startPolling(): void {
    const poll$ = this._refresh$.pipe(
      startWith(void 0),
      switchMap(() =>
        interval(2000).pipe(
          startWith(0),
          switchMap(() => this._fetchAllData())
        )
      )
    );

    poll$.pipe(takeUntilDestroyed(this._destroyRef)).subscribe();
  }

  private _startStalenessTimer(): void {
    timer(0, 1000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        if (!this.lastUpdated) {
          this.secondsAgo = 0;
          this.isStale = false;
          return;
        }

        this.secondsAgo = Math.floor((Date.now() - this.lastUpdated.getTime()) / 1000);
        this.isStale = this.secondsAgo > 10;
      });
  }

  private _fetchAllData(): Observable<unknown> {
    return forkJoin({
      account: this._apiService.getAccountSummary().pipe(
        catchError((err) => { console.error("Account fetch failed:", err); return of(null); })
      ),
      positions: this._apiService.getPositions().pipe(
        catchError((err) => { console.error("Positions fetch failed:", err); return of(null); })
      ),
      orders: this._apiService.getOpenOrders().pipe(
        catchError((err) => { console.error("Orders fetch failed:", err); return of(null); })
      )
    }).pipe(
      tap((results) => {
        const failedCount = [results.account, results.positions, results.orders].filter(r => r === null).length;

        if (failedCount === 3) {
          this._consecutiveErrors += 1;
          if (this._consecutiveErrors >= 3) {
            this.showErrorBanner = true;
            this.errorMessage = "Unable to reach Hyperliquid API. Retrying...";
          } else {
            this._snackBar.open("Failed to refresh dashboard data", "Dismiss", { duration: 3000 });
          }
        } else {
          if (failedCount > 0) {
            this._snackBar.open("Some dashboard data failed to load", "Dismiss", { duration: 3000 });
          } else {
            this._consecutiveErrors = 0;
            this.showErrorBanner = false;
          }

          if (results.account !== null) { this.accountSummary = results.account; }
          if (results.positions !== null) { this.positions = results.positions; }
          if (results.orders !== null) { this.orders = results.orders; }
          this.lastUpdated = new Date();
          this.isStale = false;
        }

        this.isLoading = false;
      })
    );
  }
}