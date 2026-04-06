import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { MatCardModule } from "@angular/material/card";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatDividerModule } from "@angular/material/divider";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { MatSliderModule } from "@angular/material/slider";
import { merge } from "rxjs";
import { PlaceOrderRequest, PlaceOrderResponse } from "../../core/models/place-order.model";
import { Position } from "../../core/models/position.model";
import { TradableAsset } from "../../core/models/tradable-asset.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { MarketDataService } from "../../core/services/market-data.service";
import { NotificationService } from "../../core/services/notification.service";
import { OrderService } from "../../core/services/order.service";
import { AgentInfo, AgentService } from "../../core/services/agent.service";
import { SignalRService } from "../../core/services/signalr.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { ConfirmDialogComponent, ConfirmDialogData } from "./confirm-dialog/confirm-dialog.component";
import { signal } from "@angular/core";

interface OrderEntryForm {
  side: FormControl<"buy" | "sell">;
  orderType: FormControl<"market" | "limit">;
  price: FormControl<number | null>;
  size: FormControl<number | null>;
  stopLossPrice: FormControl<number | null>;
  takeProfitPrice: FormControl<number | null>;
}

@Component({
  selector: "app-order-entry",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatDividerModule,
    MatSliderModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./order-entry.component.html",
  styleUrl: "./order-entry.component.scss"
})
export class OrderEntryComponent implements OnInit {
  private readonly _fb = inject(FormBuilder);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _orderService = inject(OrderService);
  private readonly _agentService = inject(AgentService);
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _dialog = inject(MatDialog);
  private readonly _notifications = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);

  public orderForm!: FormGroup<OrderEntryForm>;
  public isSubmitting = false;
  public midPrice: number | null = null;
  public markPrice: number | null = null;
  public livePrice: number | null = null;
  public leverage = 5;
  public maxLeverage = 40;
  public marginMode: "cross" | "isolated" = "cross";
  public leverageStatus: string | null = null;
  public leverageError = false;
  public readonly showSlTp = signal(false);
  public currentPositionLiquidationPrice: number | null = null;

  public assets: TradableAsset[] = [{ symbol: "BTC-PERP", name: "Bitcoin", maxLeverage: 40, szDecimals: 5 }];
  public isLoadingAssets = true;
  public selectedAsset = "BTC-PERP";
  public connectedAgents: AgentInfo[] = [];
  public selectedAgentId: string | null = null;

  public get selectedCoin(): string {
    return this.selectedAsset.replace("-PERP", "");
  }

  public get selectedAssetInfo(): TradableAsset | undefined {
    return this.assets.find(a => a.symbol === this.selectedAsset);
  }

  public ngOnInit(): void {
    this.orderForm = this._fb.group<OrderEntryForm>({
      side: this._fb.nonNullable.control("buy"),
      orderType: this._fb.nonNullable.control("limit"),
      price: this._fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
      size: this._fb.control<number | null>(null, [Validators.required, Validators.min(0.000001)]),
      stopLossPrice: this._fb.control<number | null>(null),
      takeProfitPrice: this._fb.control<number | null>(null)
    });

    this.orderForm.controls.orderType.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((orderType: "market" | "limit") => {
        if (orderType === "limit") {
          this.orderForm.controls.price.setValidators([Validators.required, Validators.min(0.01)]);
          if (this.midPrice !== null) {
            this.orderForm.controls.price.setValue(this.midPrice);
          }
        } else {
          this.orderForm.controls.price.clearValidators();
          this.orderForm.controls.price.setValue(null);
        }

        this.orderForm.controls.price.updateValueAndValidity();
        this._refreshSlTpValidation();
      });

    merge(
      this.orderForm.controls.side.valueChanges,
      this.orderForm.controls.orderType.valueChanges,
      this.orderForm.controls.price.valueChanges
    )
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._refreshSlTpValidation();
      });

    this._loadMidPrice();
    this._loadPositionContext();
    this._subscribeToPriceUpdates();

    this._orderService.getAvailableAssets().subscribe({
      next: (assets) => {
        this.assets = assets;
        this.isLoadingAssets = false;
      },
      error: () => {
        this.isLoadingAssets = false;
      }
    });

    // Subscribe to connected agents for order routing
    this._agentService.agents$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((agents) => {
        this.connectedAgents = agents.filter(a => a.state !== "disconnected");
      });

    this._agentService.selectedAgentId$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((id) => {
        this.selectedAgentId = id;
      });

    this._agentService.refreshAgents();
  }

  public onAgentChange(agentId: string | null): void {
    this._agentService.selectAgent(agentId);
  }

  public onAssetChange(asset: string): void {
    this.selectedAsset = asset;
    this.midPrice = null;
    this.markPrice = null;
    this.livePrice = null;
    this.leverageStatus = null;
    this.orderForm.controls.price.setValue(null);

    const info = this.selectedAssetInfo;
    if (info) {
      this.maxLeverage = info.maxLeverage;
      if (this.leverage > info.maxLeverage) {
        this.leverage = info.maxLeverage;
      }
    }

    this._loadMidPrice();
    this._loadPositionContext();
    this._applyLeverage();
  }

  private _loadMidPrice(): void {
    const asset = this.selectedAsset;
    this._marketDataService.getMarketInfo(asset)
      .subscribe({
        next: (marketInfo) => {
          if (this.selectedAsset !== asset) return;
          this.midPrice = marketInfo.midPrice;
          this.markPrice = marketInfo.markPrice;
          this.livePrice = marketInfo.midPrice;
          if (this.orderForm.controls.orderType.value === "limit") {
            this.orderForm.controls.price.setValue(this.midPrice);
          }

          this._refreshSlTpValidation();
        },
        error: () => {
          // Market data load error is handled by the HTTP interceptor
        }
      });
  }

  private _subscribeToPriceUpdates(): void {
    this._signalRService.priceUpdate$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((update) => {
        const coin = this.selectedAsset.replace("-PERP", "");
        const updateCoin = update.asset.replace("-PERP", "");
        if (coin.toUpperCase() === updateCoin.toUpperCase()) {
          this.livePrice = update.lastPrice;
          this._refreshSlTpValidation();
        }
      });
  }

  private _loadPositionContext(): void {
    this._apiService.getPositions(new HttpContext().set(SKIP_ERROR_NOTIFICATION, true))
      .subscribe({
        next: (positions: Position[]) => {
          const currentPosition = positions.find((position) => position.asset === this.selectedAsset && position.size !== 0);
          this.currentPositionLiquidationPrice = currentPosition?.liquidationPrice ?? null;
        },
        error: () => {
          this.currentPositionLiquidationPrice = null;
        }
      });
  }

  public isLimitOrder(): boolean {
    return this.orderForm.controls.orderType.value === "limit";
  }

  public toggleSlTp(): void {
    this.showSlTp.update((isVisible) => !isVisible);

    if (this.showSlTp()) {
      this.orderForm.controls.stopLossPrice.setValidators([Validators.min(0.01), this._createSlValidator()]);
      this.orderForm.controls.takeProfitPrice.setValidators([Validators.min(0.01), this._createTpValidator()]);
      this._refreshSlTpValidation();
      return;
    }

    this.orderForm.controls.stopLossPrice.setValue(null);
    this.orderForm.controls.takeProfitPrice.setValue(null);
    this.orderForm.controls.stopLossPrice.clearValidators();
    this.orderForm.controls.takeProfitPrice.clearValidators();
    this.orderForm.controls.stopLossPrice.updateValueAndValidity();
    this.orderForm.controls.takeProfitPrice.updateValueAndValidity();
  }

  public getPartialSlTpWarning(): string | null {
    if (!this.showSlTp()) {
      return null;
    }

    const hasStopLoss = this._hasValue(this.orderForm.controls.stopLossPrice.value);
    const hasTakeProfit = this._hasValue(this.orderForm.controls.takeProfitPrice.value);

    if (hasStopLoss && !hasTakeProfit) {
      return "Consider adding a take profit to lock in gains.";
    }

    if (!hasStopLoss && hasTakeProfit) {
      return "Consider adding a stop loss to limit downside risk.";
    }

    return null;
  }

  public getLiquidationWarning(): string | null {
    if (!this.showSlTp()) {
      return null;
    }

    const stopLossPrice = this.orderForm.controls.stopLossPrice.value;
    if (!this._hasValue(stopLossPrice) || !this._hasValue(this.currentPositionLiquidationPrice)) {
      return null;
    }

    const isBeyondLiquidation = this.orderForm.controls.side.value === "buy"
      ? stopLossPrice <= this.currentPositionLiquidationPrice
      : stopLossPrice >= this.currentPositionLiquidationPrice;

    if (!isBeyondLiquidation) {
      return null;
    }

    const liquidationPriceLabel = this.currentPositionLiquidationPrice.toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });

    return `Stop loss is beyond your liquidation price (${liquidationPriceLabel}).`;
  }

  public onLeverageChange(value: number): void {
    this.leverage = value;
    this._applyLeverage();
  }

  public onMarginModeChange(mode: "cross" | "isolated"): void {
    this.marginMode = mode;
    this._applyLeverage();
  }

  private _applyLeverage(): void {
    this.leverageStatus = "Updating...";
    this.leverageError = false;

    this._orderService
      .setLeverage({
        asset: this.selectedAsset,
        leverage: this.leverage,
        isCross: this.marginMode === "cross"
      }, new HttpContext().set(SKIP_ERROR_NOTIFICATION, true))
      .subscribe({
        next: () => {
          this.leverageStatus = `${this.leverage}x ${this.marginMode} set`;
          this.leverageError = false;
        },
        error: (err: HttpErrorResponse) => {
          this.leverageStatus = `Failed: ${formatErrorPayload(err)}`;
          this.leverageError = true;
        }
      });
  }

  public onSubmit(): void {
    if (this.orderForm.invalid || this.isSubmitting) {
      return;
    }

    const dialogData: ConfirmDialogData = {
      side: this.orderForm.controls.side.value,
      orderType: this.orderForm.controls.orderType.value,
      asset: this.selectedAsset,
      price: this.orderForm.controls.orderType.value === "limit" ? this.orderForm.controls.price.value : null,
      size: this.orderForm.controls.size.value ?? 0,
      stopLossPrice: this.orderForm.controls.stopLossPrice.value,
      takeProfitPrice: this.orderForm.controls.takeProfitPrice.value
    };

    this._dialog
      .open(ConfirmDialogComponent, { data: dialogData, width: "400px" })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (!confirmed) {
          return;
        }

        this._submitOrder();
      });
  }

  private _submitOrder(): void {
    this.isSubmitting = true;

    const request: PlaceOrderRequest = {
      asset: this.selectedAsset,
      side: this.orderForm.controls.side.value,
      orderType: this.orderForm.controls.orderType.value,
      price: this.orderForm.controls.orderType.value === "limit" ? this.orderForm.controls.price.value : null,
      size: this.orderForm.controls.size.value ?? 0,
      stopLossPrice: this.orderForm.controls.stopLossPrice.value,
      takeProfitPrice: this.orderForm.controls.takeProfitPrice.value
    };

    const agentId = this._agentService.selectedAgentId;

    // Route through agent if one is selected, otherwise fall back to direct API call
    const order$ = agentId
      ? this._agentService.placeOrderViaAgent(agentId, request)
      : this._orderService.placeOrder(request);

    order$
      .subscribe({
        next: (response: PlaceOrderResponse) => {
          this.isSubmitting = false;

          if (response.success) {
            this._notifications.success(
              agentId
                ? `Order queued for agent (Status: ${response.status})`
                : `Order placed (ID: ${response.orderId}, Status: ${response.status})`
            );

            if (response.detail?.trim()) {
              this._notifications.warning(`${response.detail}`, 7000);
            }

            return;
          }

          this._notifications.warning(`Order rejected: ${response.detail}`);
        },
        error: () => {
          this.isSubmitting = false;
        }
      });
  }

  private _createSlValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const stopLossPrice = control.value as number | null;
      if (!this._hasValue(stopLossPrice)) {
        return null;
      }

      const referencePrice = this._getReferencePrice();
      if (!this._hasValue(referencePrice)) {
        return null;
      }

      const side = this.orderForm.controls.side.value;

      if (side === "buy" && stopLossPrice >= referencePrice) {
        return { slInvalidSide: "Stop loss must be below entry price for long positions" };
      }

      if (side === "sell" && stopLossPrice <= referencePrice) {
        return { slInvalidSide: "Stop loss must be above entry price for short positions" };
      }

      return null;
    };
  }

  private _createTpValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const takeProfitPrice = control.value as number | null;
      if (!this._hasValue(takeProfitPrice)) {
        return null;
      }

      const referencePrice = this._getReferencePrice();
      if (!this._hasValue(referencePrice)) {
        return null;
      }

      const side = this.orderForm.controls.side.value;

      if (side === "buy" && takeProfitPrice <= referencePrice) {
        return { tpInvalidSide: "Take profit must be above entry price for long positions" };
      }

      if (side === "sell" && takeProfitPrice >= referencePrice) {
        return { tpInvalidSide: "Take profit must be below entry price for short positions" };
      }

      return null;
    };
  }

  private _getReferencePrice(): number | null {
    if (this.orderForm.controls.orderType.value === "limit") {
      return this.orderForm.controls.price.value;
    }

    return this.livePrice ?? this.markPrice ?? this.midPrice;
  }

  private _refreshSlTpValidation(): void {
    if (!this.showSlTp()) {
      return;
    }

    this.orderForm.controls.stopLossPrice.updateValueAndValidity({ emitEvent: false });
    this.orderForm.controls.takeProfitPrice.updateValueAndValidity({ emitEvent: false });
  }

  private _hasValue(value: number | null | undefined): value is number {
    return value !== null && value !== undefined;
  }

}