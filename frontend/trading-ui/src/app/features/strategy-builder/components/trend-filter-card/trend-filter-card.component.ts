import { Component, DestroyRef, Input, OnChanges, OnInit, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";
import { TrendFilterType, TrendOperator } from "../../models/strategy.model";
import { TREND_FILTER_OPERATORS, TrendFilterOperatorOption } from "../../enums/trend-filter-operator.enum";

@Component({
  selector: "app-trend-filter-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./trend-filter-card.component.html",
  styleUrl: "./trend-filter-card.component.scss"
})
export class TrendFilterCardComponent implements OnInit, OnChanges {
  private readonly _destroyRef = inject(DestroyRef);

  private _boundGroup: FormGroup | null = null;

  @Input() public group: FormGroup | null = null;

  public readonly filterTypes: readonly { value: TrendFilterType; label: string }[] = [
    { value: "ema_cross", label: "EMA cross" },
    { value: "sma_cross", label: "SMA cross" },
    { value: "price_above_ema", label: "Price vs EMA" },
  ];

  public readonly appliesToOptions: readonly { value: "long" | "short" | "both"; label: string }[] = [
    { value: "long", label: "Long" },
    { value: "short", label: "Short" },
    { value: "both", label: "Both" },
  ];

  public ngOnInit(): void {
    this._bindGroup();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["group"] !== undefined) {
      this._bindGroup();
    }
  }

  public get isBound(): boolean {
    return this.group !== null;
  }

  public get selectedType(): TrendFilterType | null {
    return (this.group?.get("type")?.value as TrendFilterType | null) ?? null;
  }

  public get showPeriodField(): boolean {
    return this.selectedType === "price_above_ema";
  }

  public get showFastSlowFields(): boolean {
    return this.selectedType === "ema_cross" || this.selectedType === "sma_cross";
  }

  public get availableOperators(): TrendFilterOperatorOption[] {
    const allowedOperators = this.selectedType === "price_above_ema"
      ? new Set<TrendOperator>(["above", "below", "cross_above", "cross_below"])
      : new Set<TrendOperator>(["gt", "lt", "cross_above", "cross_below"]);

    return TREND_FILTER_OPERATORS.filter((operator) => allowedOperators.has(operator.value));
  }

  public hasError(path: string, errorCode: string): boolean {
    const control = this.group?.get(path);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  private _bindGroup(): void {
    if (this.group === null || this.group === this._boundGroup) {
      return;
    }

    this._boundGroup = this.group;

    const enabledControl = this.group.get("enabled");
    const typeControl = this.group.get("type");

    enabledControl?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyControlState();
      });

    typeControl?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._ensureValidOperator();
        this._applyControlState();
      });

    this._ensureValidOperator();
    this._applyControlState();
  }

  private _applyControlState(): void {
    if (this.group === null) {
      return;
    }

    const enabled = Boolean(this.group.get("enabled")?.value);
    this._setControlEnabled("type", enabled);
    this._setControlEnabled("operator", enabled);
    this._setControlEnabled("appliesTo", enabled);
    this._setControlEnabled("fastPeriod", enabled && this.showFastSlowFields);
    this._setControlEnabled("slowPeriod", enabled && this.showFastSlowFields);
    this._setControlEnabled("period", enabled && this.showPeriodField);
  }

  private _ensureValidOperator(): void {
    if (this.group === null) {
      return;
    }

    const operatorControl = this.group.get("operator");
    const currentOperator = operatorControl?.value as TrendOperator | null;
    const validOperators = this.availableOperators.map((operator) => operator.value);

    if (operatorControl === null || (currentOperator !== null && validOperators.includes(currentOperator))) {
      return;
    }

    operatorControl.setValue(this._getDefaultOperator(), { emitEvent: false });
  }

  private _getDefaultOperator(): TrendOperator {
    return this.selectedType === "price_above_ema" ? "above" : "gt";
  }

  private _setControlEnabled(path: string, enabled: boolean): void {
    const control = this.group?.get(path);

    if (control === null || control === undefined) {
      return;
    }

    if (enabled) {
      control.enable({ emitEvent: false });
      return;
    }

    control.disable({ emitEvent: false });
  }
}