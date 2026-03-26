import { HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTabsModule } from "@angular/material/tabs";
import { ModifyOrderDto } from "../../core/models/modify-order.model";
import { PlaceOrderRequest } from "../../core/models/place-order.model";
import { Observable, Subject, forkJoin, interval, of, timer } from "rxjs";
import { catchError, startWith, switchMap, tap } from "rxjs/operators";
import { AccountSummary } from "../../core/models/account-summary.model";
import { OpenOrder } from "../../core/models/open-order.model";
import { Position } from "../../core/models/position.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { OrderService } from "../../core/services/order.service";
import { ConfirmDialogComponent } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { AccountSummaryComponent } from "./account-summary/account-summary.component";
import { OrdersTableComponent } from "./orders-table/orders-table.component";
import { ModifyOrderDialogData, ModifyOrderModalComponent } from "./orders-table/modify-order-modal/modify-order.modal.component";
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
  private readonly _dialog = inject(MatDialog);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _orderService = inject(OrderService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _refresh$ = new Subject<void>();
  private readonly _pendingOrderIds = new Set<string>();
  private readonly _pendingPositionKeys = new Set<string>();
  private _consecutiveErrors = 0;

  @ViewChild(OrdersTableComponent)
  public ordersTable?: OrdersTableComponent;

  @ViewChild(PositionsTableComponent)
  public positionsTable?: PositionsTableComponent;

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

  public onCancelOrder(order: OpenOrder): void {
    if (this.ordersTable?.isLoading(order.orderId)) {
      return;
    }

    this._dialog.open(ConfirmDialogComponent, {
      data: {
        title: "Cancel Order",
        message: `Cancel order #${order.orderId}?`,
        confirmText: "Cancel Order",
        cancelText: "Keep Order"
      },
      width: "400px"
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      const orderIndex = this.orders.findIndex((item) => item.orderId === order.orderId);
      if (orderIndex < 0) {
        return;
      }

      const removedOrder = this.orders[orderIndex];
      this.ordersTable?.setLoading(order.orderId, true);
      this._pendingOrderIds.add(order.orderId);
      this.orders = this.orders.filter((item) => item.orderId !== order.orderId);

      this._orderService.cancelOrder(order.orderId).subscribe({
        next: () => {
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
          this._snackBar.open("Order cancelled successfully", "Dismiss", { duration: 3000 });
          this._refresh$.next();
        },
        error: (errorResponse: HttpErrorResponse) => {
          this.orders = [
            ...this.orders.slice(0, orderIndex),
            removedOrder,
            ...this.orders.slice(orderIndex)
          ];
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
          this._snackBar.open(`Failed to cancel order: ${this._formatErrorPayload(errorResponse)}`, "Dismiss", {
            duration: 5000
          });
        }
      });
    });
  }

  public onCancelAllOrders(): void {
    const orderCount = this.orders.length;
    if (orderCount === 0 || this.ordersTable?.globalLoading) {
      return;
    }

    this._dialog.open(ConfirmDialogComponent, {
      data: {
        title: "Cancel All Orders",
        message: `Cancel all ${orderCount} open orders for ${this.orders[0]?.asset ?? "BTC-PERP"}?`,
        confirmText: "Cancel All",
        cancelText: "Keep Orders"
      },
      width: "400px"
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      const previousOrders = [...this.orders];
      this.ordersTable?.setGlobalLoading(true);
      this.orders = [];

      this._orderService.cancelAllOrders(this.orders[0]?.asset ?? "BTC").subscribe({
        next: () => {
          this.ordersTable?.setGlobalLoading(false);
          this._snackBar.open(`Cancelled ${orderCount} orders`, "Dismiss", { duration: 3000 });
          this._refresh$.next();
        },
        error: (errorResponse: HttpErrorResponse) => {
          this.orders = previousOrders;
          this.ordersTable?.setGlobalLoading(false);
          this._snackBar.open(`Failed to cancel orders: ${this._formatErrorPayload(errorResponse)}`, "Dismiss", {
            duration: 5000
          });
        }
      });
    });
  }

  public onModifyOrder(order: OpenOrder): void {
    if (this.ordersTable?.isLoading(order.orderId)) {
      return;
    }

    this._dialog.open(ModifyOrderModalComponent, {
      data: { order } as ModifyOrderDialogData,
      width: "400px"
    }).afterClosed().subscribe((result: ModifyOrderDto | undefined) => {
      if (result === undefined) {
        return;
      }

      const orderIndex = this.orders.findIndex((item) => item.orderId === order.orderId);
      if (orderIndex < 0) {
        return;
      }

      const originalOrder = { ...this.orders[orderIndex] };

      this.orders = this.orders.map((item) =>
        item.orderId === order.orderId
          ? { ...item, price: result.price, size: result.size }
          : item
      );
      this.ordersTable?.setLoading(order.orderId, true);
      this._pendingOrderIds.add(order.orderId);

      this._orderService.modifyOrder(order.orderId, result).subscribe({
        next: () => {
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
          this._snackBar.open("Order modified successfully", "Dismiss", { duration: 3000 });
          this._refresh$.next();
        },
        error: (errorResponse: HttpErrorResponse) => {
          this.orders = this.orders.map((item) => item.orderId === order.orderId ? originalOrder : item);
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
          this._snackBar.open(`Failed to modify order: ${this._formatErrorPayload(errorResponse)}`, "Dismiss", {
            duration: 5000
          });
        }
      });
    });
  }

  public onClosePosition(position: Position): void {
    const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;

    if (this.positionsTable?.loadingPositionKeys.has(positionKey)) {
      return;
    }

    const closeSide: "buy" | "sell" = position.side === "Long" ? "sell" : "buy";

    this._dialog.open(ConfirmDialogComponent, {
      data: {
        title: "Close Position",
        message: `Close ${position.side} ${position.asset}-PERP position?`,
        side: closeSide,
        orderType: "market" as const,
        asset: position.asset + "-PERP",
        size: Math.abs(position.size),
        confirmText: "Close Position",
        cancelText: "Keep Position"
      },
      width: "400px"
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      const positionIndex = this.positions.findIndex(
        (item) => item.asset === position.asset && item.side === position.side
      );
      if (positionIndex < 0) {
        return;
      }

      const removedPosition = this.positions[positionIndex];
      this.positionsTable?.setLoading(positionKey, true);
      this._pendingPositionKeys.add(positionKey);
      this.positions = this.positions.filter(
        (item) => !(item.asset === position.asset && item.side === position.side)
      );

      const closeRequest: PlaceOrderRequest = {
        asset: position.asset,
        side: closeSide,
        orderType: "market",
        price: null,
        size: Math.abs(position.size)
      };

      this._orderService.placeOrder(closeRequest).subscribe({
        next: () => {
          this._pendingPositionKeys.delete(positionKey);
          this.positionsTable?.setLoading(positionKey, false);
          this._snackBar.open("Position close order submitted", "Dismiss", { duration: 3000 });
          this._refresh$.next();
        },
        error: (errorResponse: HttpErrorResponse) => {
          this.positions = [
            ...this.positions.slice(0, positionIndex),
            removedPosition,
            ...this.positions.slice(positionIndex)
          ];
          this._pendingPositionKeys.delete(positionKey);
          this.positionsTable?.setLoading(positionKey, false);
          this._snackBar.open(`Failed to close position: ${this._formatErrorPayload(errorResponse)}`, "Dismiss", {
            duration: 5000
          });
        }
      });
    });
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
          if (results.positions !== null) {
            this.positions = this._pendingPositionKeys.size === 0
              ? results.positions
              : results.positions.filter((position) => !this._pendingPositionKeys.has(position.asset + position.side));
          }
          if (results.orders !== null) {
            this.orders = this._pendingOrderIds.size === 0
              ? results.orders
              : results.orders.filter(o => !this._pendingOrderIds.has(o.orderId));
          }
          this.lastUpdated = new Date();
          this.isStale = false;
        }

        this.isLoading = false;
      })
    );
  }

  private _formatErrorPayload(errorResponse: HttpErrorResponse): string {
    if (typeof errorResponse.error === "string" && errorResponse.error.length > 0) {
      return errorResponse.error;
    }

    if (errorResponse.error !== null && errorResponse.error !== undefined) {
      if (typeof errorResponse.error === "object" && errorResponse.error.errorMessage) {
        return String(errorResponse.error.errorMessage);
      }

      if (typeof errorResponse.error === "object" && errorResponse.error.detail) {
        return String(errorResponse.error.detail);
      }

      if (typeof errorResponse.error === "object" && errorResponse.error.title) {
        return String(errorResponse.error.title);
      }

      return "An unexpected error occurred";
    }

    return errorResponse.message || "Unknown error";
  }
}