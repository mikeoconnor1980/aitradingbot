import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTabsModule } from "@angular/material/tabs";
import { Router } from "@angular/router";
import { ModifyOrderDto } from "../../core/models/modify-order.model";
import { CloseAllProgress, PlaceOrderRequest } from "../../core/models/place-order.model";
import { ModifyTriggerOrderDto, PlaceTriggerOrderRequest } from "../../core/models/trigger-order.model";
import { Observable, Subject, forkJoin, interval, of, timer } from "rxjs";
import { catchError, last, startWith, switchMap, tap } from "rxjs/operators";
import { HttpContext } from "@angular/common/http";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { AccountSummary } from "../../core/models/account-summary.model";
import { OpenOrder } from "../../core/models/open-order.model";
import { Position } from "../../core/models/position.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { NotificationService } from "../../core/services/notification.service";
import { OrderService } from "../../core/services/order.service";
import { AccountStateService } from "../../core/services/account-state.service";
import { AgentService } from "../../core/services/agent.service";
import { LayoutService } from "../../core/services/layout.service";
import { ResponsiveDialogService } from "../../core/services/responsive-dialog.service";
import { ConfirmDialogComponent } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { AccountSummaryComponent } from "./account-summary/account-summary.component";
import { CloseAllDialogComponent, CloseAllResult } from "./positions-table/close-all-dialog/close-all-dialog.component";
import { OrdersTableComponent } from "./orders-table/orders-table.component";
import { ModifyOrderDialogData, ModifyOrderModalComponent } from "./orders-table/modify-order-modal/modify-order.modal.component";
import { SetSlTpDialogData, SetSlTpModalComponent, SetSlTpResult } from "./positions-table/set-sltp-modal/set-sltp.modal.component";
import { PositionsTableComponent } from "./positions-table/positions-table.component";
import { ActivityFeedComponent } from "./activity-feed/activity-feed.component";
import { MarketContextCardComponent } from "./market-context-card/market-context-card.component";

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AccountSummaryComponent,
    PositionsTableComponent,
    OrdersTableComponent,
    ActivityFeedComponent,
    MarketContextCardComponent
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss"
})
export class DashboardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _responsiveDialog = inject(ResponsiveDialogService);
  private readonly _router = inject(Router);
  private readonly _layout = inject(LayoutService);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _orderService = inject(OrderService);
  private readonly _notifications = inject(NotificationService);
  private readonly _accountState = inject(AccountStateService);
  private readonly _agentService = inject(AgentService);
  private readonly _refresh$ = new Subject<void>();
  private readonly _pendingOrderIds = new Set<string>();
  private readonly _pendingPositionKeys = new Set<string>();
  private _consecutiveErrors = 0;

  public readonly isMobile = this._layout.isMobile;

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
    this._accountState.positions$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((positions) => { this.positions = positions; });
    this._accountState.orders$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((orders) => { this.orders = orders; });
  }

  public onManualRefresh(): void {
    this._refresh$.next();
  }

  public onFabClick(): void {
    this._router.navigate(["/order-entry"]);
  }

  public onCancelOrder(order: OpenOrder): void {
    if (this.ordersTable?.isLoading(order.orderId)) {
      return;
    }

    this._responsiveDialog.open(ConfirmDialogComponent, {
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
          this._notifications.success('Order cancelled successfully');
          this._refresh$.next();
        },
        error: () => {
          this.orders = [
            ...this.orders.slice(0, orderIndex),
            removedOrder,
            ...this.orders.slice(orderIndex)
          ];
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
        }
      });
    });
  }

  public onCancelAllOrders(): void {
    const orderCount = this.orders.length;
    if (orderCount === 0 || this.ordersTable?.globalLoading) {
      return;
    }

    this._responsiveDialog.open(ConfirmDialogComponent, {
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
      const uniqueAssets = [...new Set(previousOrders.map(o => o.asset))];
      this.ordersTable?.setGlobalLoading(true);
      this.orders = [];

      const cancelRequests = uniqueAssets.map(asset => this._orderService.cancelAllOrders(asset));
      forkJoin(cancelRequests).subscribe({
        next: () => {
          this.ordersTable?.setGlobalLoading(false);
          this._notifications.success(`Cancelled ${orderCount} orders`);
          this._refresh$.next();
        },
        error: () => {
          this.orders = previousOrders;
          this.ordersTable?.setGlobalLoading(false);
        }
      });
    });
  }

  public onModifyOrder(order: OpenOrder): void {
    if (this.ordersTable?.isLoading(order.orderId)) {
      return;
    }

    this._responsiveDialog.open(ModifyOrderModalComponent, {
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
          this._notifications.success('Order modified successfully');
          this._refresh$.next();
        },
        error: () => {
          this.orders = this.orders.map((item) => item.orderId === order.orderId ? originalOrder : item);
          this._pendingOrderIds.delete(order.orderId);
          this.ordersTable?.setLoading(order.orderId, false);
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

    this._responsiveDialog.open(ConfirmDialogComponent, {
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

      const agentId = this._agentService.selectedAgentId;
      const close$ = agentId
        ? this._agentService.placeOrderViaAgent(agentId, closeRequest)
        : this._orderService.placeOrder(closeRequest);

      close$.subscribe({
        next: () => {
          this._pendingPositionKeys.delete(positionKey);
          this.positionsTable?.setLoading(positionKey, false);
          this._notifications.success('Position close order submitted');
          this._refresh$.next();
        },
        error: () => {
          this.positions = [...this.positions, removedPosition];
          this._pendingPositionKeys.delete(positionKey);
          this.positionsTable?.setLoading(positionKey, false);
        }
      });
    });
  }

  public onCloseAllPositions(): void {
    const currentPositions = [...this.positions];
    if (currentPositions.length === 0 || this.positionsTable?.globalLoading) {
      return;
    }

    this._responsiveDialog.open(CloseAllDialogComponent, {
      data: { positions: currentPositions },
      width: "450px"
    }).afterClosed().subscribe((result: CloseAllResult | undefined) => {
      if (!result?.confirmed) {
        return;
      }

      const savedPositions = [...this.positions];
      this.positionsTable?.setGlobalLoading(true);
      currentPositions.forEach((position) => this._pendingPositionKeys.add(position.asset + position.side));
      this.positions = [];

      const agentId = this._agentService.selectedAgentId;

      if (agentId) {
        // Route each close through the agent as individual PlaceOrder commands
        const closeRequests = currentPositions.map((p) => {
          const req: PlaceOrderRequest = {
            asset: p.asset,
            side: p.side === "Long" ? "sell" : "buy",
            orderType: "market",
            price: null,
            size: Math.abs(p.size)
          };
          return this._agentService.placeOrderViaAgent(agentId, req);
        });

        forkJoin(closeRequests).subscribe({
          next: () => {
            currentPositions.forEach((position) => this._pendingPositionKeys.delete(position.asset + position.side));
            this.positionsTable?.setGlobalLoading(false);
            this._notifications.success(`Queued ${currentPositions.length} close orders for agent`);
            this._refresh$.next();
          },
          error: () => {
            currentPositions.forEach((position) => this._pendingPositionKeys.delete(position.asset + position.side));
            this.positionsTable?.setGlobalLoading(false);
            this._notifications.error("Failed to queue close orders");
            this.positions = savedPositions;
            this._refresh$.next();
          }
        });
      } else {
        this._orderService.closeAllPositions(currentPositions).pipe(last()).subscribe({
          next: (progress: CloseAllProgress) => {
            currentPositions.forEach((position) => this._pendingPositionKeys.delete(position.asset + position.side));
            this.positionsTable?.setGlobalLoading(false);

            if (progress.failed === 0) {
              this._notifications.success(`Closed ${progress.succeeded} positions`);
            } else if (progress.succeeded === 0) {
              this._notifications.error("Failed to close positions");
              this.positions = savedPositions;
            } else {
              this._notifications.warning(`Closed ${progress.succeeded}/${progress.total} positions (${progress.failed} failed)`);
            }

            this._refresh$.next();
          },
          error: () => {
            currentPositions.forEach((position) => this._pendingPositionKeys.delete(position.asset + position.side));
            this.positionsTable?.setGlobalLoading(false);
            this._notifications.error("Failed to close positions");
            this.positions = savedPositions;
            this._refresh$.next();
          }
        });
      }
    });
  }

  public onSetSlTp(position: Position): void {
    if (this.positionsTable?.isLoading(position)) {
      return;
    }

    const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;

    this._responsiveDialog.open(SetSlTpModalComponent, {
      data: { position } as SetSlTpDialogData,
      width: "400px"
    }).afterClosed().subscribe((result: SetSlTpResult | undefined) => {
      if (result === undefined) {
        return;
      }

      const requests = this._buildTriggerOrderRequests(position, result);
      if (requests.length === 0) {
        return;
      }

      this.positionsTable?.setLoading(positionKey, true);

      forkJoin(requests).subscribe({
        next: () => {
          this.positionsTable?.setLoading(positionKey, false);
          this._notifications.success("SL/TP set successfully");
          this._refresh$.next();
        },
        error: () => {
          this.positionsTable?.setLoading(positionKey, false);
          this._refresh$.next();
        }
      });
    });
  }

  public onEditSlTp(event: { position: Position; field: "sl" | "tp"; newPrice?: number }): void {
    const { position, field, newPrice } = event;
    const orderId = field === "sl" ? position.stopLossOrderId : position.takeProfitOrderId;

    if (!orderId || newPrice == null || this.positionsTable?.isLoading(position)) {
      return;
    }

    const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;
    const request: ModifyTriggerOrderDto = {
      triggerPrice: newPrice,
      size: Math.abs(position.size)
    };

    this.positionsTable?.setLoading(positionKey, true);

    const agentId = this._agentService.selectedAgentId;
    const closingSide = this._getClosingSide(position);
    const modify$: Observable<unknown> = agentId
      ? this._agentService.modifyTriggerOrderViaAgent(
          agentId, orderId, position.asset, closingSide, newPrice, Math.abs(position.size), field)
      : this._orderService.modifyTriggerOrder(orderId, request);

    modify$.subscribe({
      next: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.success(field === "sl" ? "Stop loss updated" : "Take profit updated");
        this._refresh$.next();
      },
      error: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._refresh$.next();
      }
    });
  }

  public onRemoveSlTp(event: { position: Position; field: "sl" | "tp" }): void {
    const { position, field } = event;
    const orderId = field === "sl" ? position.stopLossOrderId : position.takeProfitOrderId;

    if (!orderId || this.positionsTable?.isLoading(position)) {
      return;
    }

    const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;
    this.positionsTable?.setLoading(positionKey, true);

    const agentId = this._agentService.selectedAgentId;
    const cancel$: Observable<unknown> = agentId
      ? this._agentService.cancelTriggerOrderViaAgent(agentId, orderId, position.asset)
      : this._orderService.cancelTriggerOrder(orderId);

    cancel$.subscribe({
      next: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.success(field === "sl" ? "Stop loss removed" : "Take profit removed");
        this._refresh$.next();
      },
      error: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._refresh$.next();
      }
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
    const skipCtx = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
    return forkJoin({
      account: this._apiService.getAccountSummary(skipCtx).pipe(
        catchError(() => of(null))
      ),
      positions: this._apiService.getPositions(skipCtx).pipe(
        catchError(() => of(null))
      ),
      orders: this._apiService.getOpenOrders(skipCtx).pipe(
        catchError(() => of(null))
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
            this._notifications.warning('Failed to refresh dashboard data');
          }
        } else {
          this._consecutiveErrors = 0;
          this.showErrorBanner = false;

          if (failedCount > 0) {
            this._notifications.warning('Some dashboard data failed to load');
          }

          if (results.account !== null) { this.accountSummary = results.account; }
          if (results.positions !== null) {
            const newPositions = this._pendingPositionKeys.size === 0
              ? results.positions
              : results.positions.filter((position) => !this._pendingPositionKeys.has(position.asset + position.side));
            this._accountState.updatePositions(newPositions);
          }
          if (results.orders !== null) {
            const newOrders = this._pendingOrderIds.size === 0
              ? results.orders
              : results.orders.filter(o => !this._pendingOrderIds.has(o.orderId));
            this._accountState.updateOrders(newOrders);
          }
          this.lastUpdated = new Date();
          this.isStale = false;
        }

        this.isLoading = false;
      })
    );
  }

  private _buildTriggerOrderRequests(position: Position, result: SetSlTpResult): Observable<unknown>[] {
    const closingSide = this._getClosingSide(position);
    const size = Math.abs(position.size);
    const requests: Observable<unknown>[] = [];
    const agentId = this._agentService.selectedAgentId;

    if (result.stopLossPrice != null) {
      const request: PlaceTriggerOrderRequest = {
        asset: position.asset,
        side: closingSide,
        size,
        triggerPrice: result.stopLossPrice,
        tpslType: "sl"
      };
      requests.push(agentId
        ? this._agentService.placeTriggerOrderViaAgent(agentId, request)
        : this._orderService.placeTriggerOrder(request));
    }

    if (result.takeProfitPrice != null) {
      const request: PlaceTriggerOrderRequest = {
        asset: position.asset,
        side: closingSide,
        size,
        triggerPrice: result.takeProfitPrice,
        tpslType: "tp"
      };
      requests.push(agentId
        ? this._agentService.placeTriggerOrderViaAgent(agentId, request)
        : this._orderService.placeTriggerOrder(request));
    }

    return requests;
  }

  private _getClosingSide(position: Position): "buy" | "sell" {
    return this._isLongPosition(position) ? "sell" : "buy";
  }

  private _isLongPosition(position: Position): boolean {
    return position.side === "Long" || position.size > 0;
  }

}