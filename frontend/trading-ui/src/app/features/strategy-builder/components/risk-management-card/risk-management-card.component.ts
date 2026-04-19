import { DecimalPipe } from "@angular/common";
import { Component, DestroyRef, Input, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { catchError, of } from "rxjs";
import { HyperliquidApiService } from "../../../../core/services/hyperliquid-api.service";
import { SubscriptionService } from "../../../../core/services/subscription.service";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-risk-management-card",
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./risk-management-card.component.html",
  styleUrl: "./risk-management-card.component.scss"
})
export class RiskManagementCardComponent implements OnInit {
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _subscriptionService = inject(SubscriptionService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _maintenanceMarginRate = 0.01;

  @Input({ required: true }) public group!: FormGroup;
  @Input() public exitGroup: FormGroup | null = null;

  public equity = 0;
  public rAmount = 0;
  public positionSize = 0;
  public derivedLeverage = 0;
  public marginRequired = 0;
  public estLiquidationPercent = 0;

  public ngOnInit(): void {
    this._syncPositionSizeType();
    this._syncAutoLeverage();
    this._subscribeToPreviewInputs();
    this._fetchEquity();
  }

  public get isRiskBased(): boolean {
    return this.group.get("positionSizeType")?.value === "risk_based";
  }

  public get showPositionSizeValue(): boolean {
    return !this.isRiskBased;
  }

  public get showLeverage(): boolean {
    if (!this.isRiskBased) {
      return true;
    }

    return !this.group.get("autoLeverage")?.value;
  }

  public get leverageLimit(): number {
    return this._subscriptionService.currentStatus?.maxLeverage ?? 50;
  }

  public get showRiskWarning(): boolean {
    const riskPercent = Number(this.group.get("riskPerTradePercent")?.value ?? 0);
    return this.isRiskBased && riskPercent > 5;
  }

  public get showPreview(): boolean {
    return this.isRiskBased && this.equity > 0 && this.stopLossPercent > 0;
  }

  public get noEquity(): boolean {
    return this.equity <= 0;
  }

  public get stopLossPercent(): number {
    if (this.exitGroup === null) {
      return 0;
    }

    const stopLossEnabled = Boolean(this.exitGroup.get("stopLoss.enabled")?.value);
    const stopLossType = String(this.exitGroup.get("stopLoss.type")?.value ?? "");
    const stopLossValue = Number(this.exitGroup.get("stopLoss.value")?.value ?? 0);

    if (!stopLossEnabled || stopLossType !== "fixed_percent" || stopLossValue <= 0) {
      return 0;
    }

    return stopLossValue;
  }

  public get stopLossNotEnabled(): boolean {
    if (this.exitGroup === null) {
      return true;
    }

    return !this.exitGroup.get("stopLoss.enabled")?.value;
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  private _syncPositionSizeType(): void {
    const typeControl = this.group.get("positionSizeType");
    if (typeControl === null) {
      return;
    }

    typeControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyPositionSizeType();
      });

    this._applyPositionSizeType();
  }

  private _applyPositionSizeType(): void {
    const isRiskBased = this.group.get("positionSizeType")?.value === "risk_based";
    const positionSizeValueControl = this.group.get("positionSizeValue");
    const riskPerTradePercentControl = this.group.get("riskPerTradePercent");
    const autoLeverageControl = this.group.get("autoLeverage");

    if (isRiskBased) {
      positionSizeValueControl?.disable();
      riskPerTradePercentControl?.enable();
      autoLeverageControl?.enable();
    } else {
      positionSizeValueControl?.enable();
      riskPerTradePercentControl?.disable();
      autoLeverageControl?.disable();
    }

    this._applyAutoLeverage();
    this._recalcPreview();
  }

  private _syncAutoLeverage(): void {
    const autoLeverageControl = this.group.get("autoLeverage");
    if (autoLeverageControl === null) {
      return;
    }

    autoLeverageControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyAutoLeverage();
      });
  }

  private _applyAutoLeverage(): void {
    const isRiskBased = this.group.get("positionSizeType")?.value === "risk_based";
    const autoLeverageOn = Boolean(this.group.get("autoLeverage")?.value);
    const leverageControl = this.group.get("leverage");

    if (isRiskBased && autoLeverageOn) {
      leverageControl?.disable();
      this._recalcPreview();
      return;
    }

    leverageControl?.enable();
    if (Number(leverageControl?.value ?? 1) > this.leverageLimit) {
      leverageControl?.setValue(this.leverageLimit);
    }
    this._recalcPreview();
  }

  private _fetchEquity(): void {
    this._apiService.getAccountSummary()
      .pipe(catchError(() => of(null)))
      .subscribe((summary) => {
        this.equity = summary?.equity ?? 0;
        this._recalcPreview();
      });
  }

  private _subscribeToPreviewInputs(): void {
    this.group.get("riskPerTradePercent")?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._recalcPreview();
      });

    this.group.get("leverage")?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._recalcPreview();
      });

    if (this.exitGroup === null) {
      return;
    }

    this.exitGroup.get("stopLoss.enabled")?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._recalcPreview();
      });

    this.exitGroup.get("stopLoss.type")?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._recalcPreview();
      });

    this.exitGroup.get("stopLoss.value")?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._recalcPreview();
      });
  }

  private _recalcPreview(): void {
    const riskPercent = Number(this.group.get("riskPerTradePercent")?.value ?? 0);
    const stopLossPercent = this.stopLossPercent;
    const autoLeverageOn = Boolean(this.group.get("autoLeverage")?.value);

    if (!this.isRiskBased || this.equity <= 0 || riskPercent <= 0 || stopLossPercent <= 0) {
      this.rAmount = 0;
      this.positionSize = 0;
      this.derivedLeverage = 0;
      this.marginRequired = 0;
      this.estLiquidationPercent = 0;
      return;
    }

    this.rAmount = this.equity * (riskPercent / 100);
    this.positionSize = this.rAmount / (stopLossPercent / 100);

    if (autoLeverageOn) {
      this.derivedLeverage = Math.floor(1 / (stopLossPercent / 100 + this._maintenanceMarginRate));
    } else {
      this.derivedLeverage = Number(this.group.get("leverage")?.value ?? 1);
    }

    this.marginRequired = this.derivedLeverage > 0
      ? this.positionSize / this.derivedLeverage
      : this.positionSize;
    this.estLiquidationPercent = stopLossPercent + (this._maintenanceMarginRate * 100);
  }
}