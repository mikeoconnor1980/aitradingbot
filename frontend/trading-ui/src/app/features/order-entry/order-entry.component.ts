import { HttpErrorResponse } from "@angular/common/http";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { MatCardModule } from "@angular/material/card";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { PlaceOrderRequest, PlaceOrderResponse } from "../../core/models/place-order.model";
import { MarketDataService } from "../../core/services/market-data.service";
import { OrderService } from "../../core/services/order.service";
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
    ReactiveFormsModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./order-entry.component.html",
  styleUrl: "./order-entry.component.scss"
})
export class OrderEntryComponent implements OnInit {
  private readonly _fb = inject(FormBuilder);
  private readonly _orderService = inject(OrderService);
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _dialog = inject(MatDialog);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _destroyRef = inject(DestroyRef);

  public orderForm!: FormGroup<OrderEntryForm>;
  public isSubmitting = false;
  public midPrice: number | null = null;

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

    this._marketDataService.getMarketInfo("BTC-PERP")
      .subscribe({
        next: (marketInfo) => {
          this.midPrice = marketInfo.midPrice;
          if (this.orderForm.controls.orderType.value === "limit" && this.orderForm.controls.price.value === null) {
            this.orderForm.controls.price.setValue(this.midPrice);
          }
        },
        error: () => {
          this._snackBar.open("Failed to load market data. Enter price manually.", "Dismiss", { duration: 5000 });
        }
      });
  }

  public isLimitOrder(): boolean {
    return this.orderForm.controls.orderType.value === "limit";
  }

  public onSubmit(): void {
    if (this.orderForm.invalid || this.isSubmitting) {
      return;
    }

    const dialogData: ConfirmDialogData = {
      side: this.orderForm.controls.side.value,
      orderType: this.orderForm.controls.orderType.value,
      asset: "BTC-PERP",
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
      asset: "BTC-PERP",
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
            this._snackBar.open(
              `Order placed (ID: ${response.orderId}, Status: ${response.status})`,
              "Dismiss",
              { duration: 5000 }
            );
            return;
          }

          this._snackBar.open(`Order rejected: ${response.detail}`, "Dismiss", { duration: 8000 });
        },
        error: (errorResponse: HttpErrorResponse) => {
          this.isSubmitting = false;
          this._snackBar.open(`Order submission failed: ${this._formatErrorPayload(errorResponse)}`, "Dismiss", {
            duration: 10000
          });
        }
      });
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