import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
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
import { PlaceOrderRequest, PlaceOrderResponse } from "../../core/models/place-order.model";
import { TradableAsset } from "../../core/models/tradable-asset.model";
import { MarketDataService } from "../../core/services/market-data.service";
import { NotificationService } from "../../core/services/notification.service";
import { OrderService } from "../../core/services/order.service";
import { SignalRService } from "../../core/services/signalr.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { ConfirmDialogComponent, ConfirmDialogData } from "./confirm-dialog/confirm-dialog.component";

interface OrderEntryForm {
  side: FormControl<"buy" | "sell">;
  orderType: FormControl<"market" | "limit">;
  price: FormControl<number | null>;
  size: FormControl<number | null>;
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
  private readonly _orderService = inject(OrderService);
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _dialog = inject(MatDialog);
  private readonly _notifications = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);

  public orderForm!: FormGroup<OrderEntryForm>;
  public isSubmitting = false;
  public midPrice: number | null = null;
  public livePrice: number | null = null;
  public leverage = 5;
  public maxLeverage = 40;
  public marginMode: "cross" | "isolated" = "cross";
  public leverageStatus: string | null = null;
  public leverageError = false;

  public assets: TradableAsset[] = [{ symbol: "BTC-PERP", name: "Bitcoin", maxLeverage: 40, szDecimals: 5 }];
  public isLoadingAssets = true;
  public selectedAsset = "BTC-PERP";

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
      size: this._fb.control<number | null>(null, [Validators.required, Validators.min(0.000001)])
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
      });

    this._loadMidPrice();
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
  }

  public onAssetChange(asset: string): void {
    this.selectedAsset = asset;
    this.midPrice = null;
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
    this._applyLeverage();
  }

  private _loadMidPrice(): void {
    this._marketDataService.getMarketInfo(this.selectedAsset)
      .subscribe({
        next: (marketInfo) => {
          this.midPrice = marketInfo.midPrice;
          this.livePrice = marketInfo.midPrice;
          if (this.orderForm.controls.orderType.value === "limit") {
            this.orderForm.controls.price.setValue(this.midPrice);
          }
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
        }
      });
  }

  public isLimitOrder(): boolean {
    return this.orderForm.controls.orderType.value === "limit";
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
      size: this.orderForm.controls.size.value ?? 0
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
      size: this.orderForm.controls.size.value ?? 0
    };

    this._orderService.placeOrder(request)
      .subscribe({
        next: (response: PlaceOrderResponse) => {
          this.isSubmitting = false;

          if (response.success) {
            this._notifications.success(`Order placed (ID: ${response.orderId}, Status: ${response.status})`);
            return;
          }

          this._notifications.warning(`Order rejected: ${response.detail}`);
        },
        error: () => {
          this.isSubmitting = false;
          // HTTP error handled by the global interceptor
        }
      });
  }

}