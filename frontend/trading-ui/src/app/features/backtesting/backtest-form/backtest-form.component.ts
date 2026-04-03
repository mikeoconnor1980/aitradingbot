import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { Component, DestroyRef, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { catchError, distinctUntilChanged, map, of, startWith, switchMap } from "rxjs";
import { BacktestEntryConditionConfig, BacktestRequest, BacktestResult, BacktestRsiParams, BacktestStrategyConfig } from "../../../core/models/backtest.model";
import { NotificationService } from "../../../core/services/notification.service";
import { EntryConditionConfig, RsiParams, StrategyConfig, StrategyDto, StrategySummaryDto } from "../../strategy-builder/models/strategy.model";
import { StrategyApiService } from "../../strategy-builder/services/strategy-api.service";

interface BacktestFormModel {
  strategyId: FormControl<string>;
  startDate: FormControl<Date | null>;
  endDate: FormControl<Date | null>;
  makerFee: FormControl<number>;
  takerFee: FormControl<number>;
  slippage: FormControl<number>;
  initialCapital: FormControl<number>;
  enableAuditLog: FormControl<boolean>;
}

export interface CoverageValidationRequest {
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
}

type BacktestControlName = keyof BacktestFormModel;

type StrategySelectionState =
  | { kind: "empty" }
  | { kind: "loaded"; strategy: StrategyDto }
  | { kind: "error"; strategyId: string };

const REQUIRED_BACKTEST_INTERVALS = ["15m", "1h", "4h"] as const;

function normalizeDateOnly(date: Date): Date {
  const normalizedDate = new Date(date);
  normalizedDate.setHours(0, 0, 0, 0);
  return normalizedDate;
}

function futureDateValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;

  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }

  return normalizeDateOnly(value) > normalizeDateOnly(new Date())
    ? { futureDate: true }
    : null;
}

function dateRangeValidator(control: AbstractControl): ValidationErrors | null {
  const formGroup = control as FormGroup<BacktestFormModel>;
  const startDate = formGroup.controls.startDate.value;
  const endDate = formGroup.controls.endDate.value;

  if (startDate === null || endDate === null) {
    return null;
  }

  return startDate < endDate ? null : { dateRange: true };
}

@Component({
  selector: "app-backtest-form",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: "./backtest-form.component.html",
  styleUrl: "./backtest-form.component.scss"
})
export class BacktestFormComponent implements OnInit, OnChanges {
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _notificationService = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);

  @Input()
  public isRunning = false;

  @Input()
  public isValidating = false;

  @Input()
  public prefillConfig: BacktestResult | null = null;

  @Input()
  public validationErrorMessage: string | null = null;

  @Input()
  public strategyId: string | null = null;

  @Output()
  public runBacktest = new EventEmitter<BacktestRequest>();

  @Output()
  public validateData = new EventEmitter<CoverageValidationRequest>();

  public strategies: StrategySummaryDto[] = [];
  public selectedStrategy: StrategyDto | null = null;
  public unavailableStrategySnapshot: BacktestResult | null = null;
  public isLoadingStrategies = false;
  public isLoadingStrategy = false;
  public readonly maxSelectableDate = normalizeDateOnly(new Date());
  public readonly form = new FormGroup<BacktestFormModel>({
    strategyId: new FormControl<string>("", { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl<Date | null>(null, { validators: [Validators.required, futureDateValidator] }),
    endDate: new FormControl<Date | null>(null, { validators: [Validators.required, futureDateValidator] }),
    makerFee: new FormControl<number>(0.0001, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    takerFee: new FormControl<number>(0.00035, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    slippage: new FormControl<number>(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    initialCapital: new FormControl<number>(10000, { nonNullable: true, validators: [Validators.required, Validators.min(100)] }),
    enableAuditLog: new FormControl<boolean>(true, { nonNullable: true })
  }, { validators: [dateRangeValidator] });
  public submitted = false;
  public formLevelError: string | null = null;

  public ngOnInit(): void {
    this._loadStrategies();

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._clearValidationErrors();
      });

    this.form.controls.strategyId.valueChanges
      .pipe(
        takeUntilDestroyed(this._destroyRef),
        startWith(this.form.controls.strategyId.value),
        distinctUntilChanged(),
        switchMap((strategyId: string) => {
          this.formLevelError = null;

          if (strategyId.trim().length === 0) {
            return of<StrategySelectionState>({ kind: "empty" });
          }

          this.isLoadingStrategy = true;

          return this._strategyApi.getStrategy(strategyId).pipe(
            map((strategy: StrategyDto): StrategySelectionState => ({ kind: "loaded", strategy })),
            catchError(() => of<StrategySelectionState>({ kind: "error", strategyId }))
          );
        })
      )
      .subscribe((state: StrategySelectionState) => {
        switch (state.kind) {
          case "empty":
            this.selectedStrategy = null;
            this.unavailableStrategySnapshot = null;
            this.isLoadingStrategy = false;
            return;
          case "loaded":
            this.selectedStrategy = state.strategy;
            this.unavailableStrategySnapshot = null;
            this.isLoadingStrategy = false;
            this.formLevelError = null;
            return;
          case "error":
            this.selectedStrategy = null;
            this.isLoadingStrategy = false;
            this.unavailableStrategySnapshot = this.prefillConfig?.strategyId === state.strategyId
              ? this.prefillConfig
              : null;
            this.form.controls.strategyId.setValue("", { emitEvent: false });
            this.form.controls.strategyId.markAsTouched();
            this.formLevelError = this.unavailableStrategySnapshot !== null
              ? "The saved strategy is no longer available. Backtest settings were restored from the historical snapshot."
              : "The selected strategy could not be loaded.";
            this._notificationService.error("Strategy not found. Please select a different strategy.");
            return;
        }
      });
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["prefillConfig"] && this.prefillConfig !== null) {
      this._prefillFromResult(this.prefillConfig);
    }

    if (changes["strategyId"]) {
      this._applyStrategyId(this.strategyId);
    }

    if (changes["validationErrorMessage"]) {
      this._applyValidationError(this.validationErrorMessage);
    }
  }

  public get isFormValid(): boolean {
    return this.form.valid && (this.selectedStrategy !== null || this.unavailableStrategySnapshot !== null);
  }

  public get canValidateCoverage(): boolean {
    return (this.selectedStrategy !== null || this.unavailableStrategySnapshot !== null) &&
      this.form.controls.startDate.valid &&
      this.form.controls.endDate.valid &&
      !this.form.hasError("dateRange") &&
      !this.isLoadingStrategy;
  }

  public get selectedIntervals(): string[] {
    if (this.selectedStrategy !== null) {
      return [...REQUIRED_BACKTEST_INTERVALS];
    }

    return this.unavailableStrategySnapshot?.intervals ?? [];
  }

  public get primaryTimeframe(): string {
    return this.selectedStrategy?.config.timeframe
      ?? this.unavailableStrategySnapshot?.strategyConfig.timeframe
      ?? "Not configured";
  }

  public get previewStrategyConfig(): StrategyConfig | BacktestStrategyConfig | null {
    return this.selectedStrategy?.config ?? this.unavailableStrategySnapshot?.strategyConfig ?? null;
  }

  public get strategyModeLabel(): string {
    const strategyMode = this.previewStrategyConfig?.strategyMode;

    if (strategyMode === undefined || strategyMode === null || strategyMode.length === 0) {
      return "Not configured";
    }

    return strategyMode.charAt(0).toUpperCase() + strategyMode.slice(1);
  }

  public get isSignalStrategy(): boolean {
    return this.previewStrategyConfig?.strategyMode === "signal";
  }

  public get previewEntryLogicLabel(): string {
    const entryLogic = this.previewStrategyConfig?.entryLogic;

    if (entryLogic === undefined || entryLogic === null || entryLogic.length === 0) {
      return "Not configured";
    }

    return entryLogic === "all" ? "All conditions" : entryLogic === "any" ? "Any condition" : entryLogic;
  }

  public get previewEntryConditionsLabel(): string {
    const conditions = this.previewStrategyConfig?.entryConditions;

    if (conditions === undefined || conditions === null || conditions.length === 0) {
      return "Not configured";
    }

    return conditions
      .filter((condition) => condition.enabled !== false)
      .map((condition) => this._formatConditionSummary(condition))
      .join("; ");
  }

  public get previewConditionCountLabel(): string {
    const conditions = this.previewStrategyConfig?.entryConditions;

    if (conditions === undefined || conditions === null || conditions.length === 0) {
      return "0 conditions";
    }

    const enabledCount = conditions.filter((condition) => condition.enabled !== false).length;
    return enabledCount === 1 ? "1 active condition" : `${enabledCount} active conditions`;
  }

  public get supportingTimeframesLabel(): string {
    const supportingTimeframes = this.selectedIntervals.filter((interval) => interval !== this.primaryTimeframe);

    return supportingTimeframes.length > 0
      ? supportingTimeframes.join(", ")
      : "None";
  }

  public get entryModeLabel(): string {
    const entryMode = this.selectedStrategy?.config.grid?.entryMode ?? this.unavailableStrategySnapshot?.strategyConfig.grid?.entryMode;

    if (entryMode === undefined || entryMode === null) {
      return "Not configured";
    }

    return entryMode
      .split("_")
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(" ");
  }

  public get positionSizeLabel(): string {
    const risk = this.selectedStrategy?.config.risk ?? this.unavailableStrategySnapshot?.strategyConfig.risk;

    if (risk === undefined || risk === null) {
      return "Not configured";
    }

    return risk.positionSizeType === "percent_wallet"
      ? `${risk.positionSizeValue}% wallet`
      : `$${risk.positionSizeValue} fixed notional`;
  }

  private _formatConditionSummary(condition: EntryConditionConfig | BacktestEntryConditionConfig): string {
    if (condition.type === "rsi") {
      const params = condition.params as RsiParams | BacktestRsiParams;
      return `RSI(${params.period}) ${this._describeRsiOperator(params.operator)} ${params.value}`;
    }

    return condition.label.length > 0 ? condition.label : condition.type;
  }

  private _describeRsiOperator(operator: string): string {
    switch (operator) {
      case "lt":
        return "is below";
      case "lte":
        return "is at or below";
      case "gt":
        return "is above";
      case "gte":
        return "is at or above";
      case "cross_above":
        return "crosses above";
      case "cross_below":
        return "crosses below";
      default:
        return operator;
    }
  }

  public hasControlError(controlName: BacktestControlName): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty || this.submitted);
  }

  public hasDateRangeError(): boolean {
    return (this.form.hasError("dateRange") || this.form.hasError("serverDateRange")) &&
      (this.form.controls.startDate.touched || this.form.controls.endDate.touched || this.submitted);
  }

  public getControlErrorMessage(controlName: BacktestControlName): string {
    const errors = this.form.controls[controlName].errors;

    if (errors?.["server"]) {
      return this.validationErrorMessage ?? "Invalid value.";
    }

    if (errors?.["required"]) {
      return "This field is required.";
    }

    if (errors?.["futureDate"]) {
      return "Future dates are not allowed.";
    }

    if (errors?.["min"]) {
      const requiredMin = errors["min"]["min"];
      return `Must be at least ${requiredMin}.`;
    }

    return "Invalid value.";
  }

  public getDateRangeErrorMessage(): string {
    if (this.form.hasError("serverDateRange")) {
      return this.validationErrorMessage ?? "End date must be after start date.";
    }

    return "End date must be after start date.";
  }

  public onRunBacktest(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (!this.isFormValid) {
      return;
    }

    const formValue = this.form.getRawValue();
    const startDate = formValue.startDate;
    const endDate = formValue.endDate;

    if (startDate === null || endDate === null) {
      return;
    }

    if (this.selectedStrategy === null && this.unavailableStrategySnapshot !== null) {
      this.runBacktest.emit({
        symbol: this.unavailableStrategySnapshot.symbol,
        intervals: this.unavailableStrategySnapshot.intervals,
        startDate: startDate.toISOString(),
        endDate: endDate.toISOString(),
        initialCapital: formValue.initialCapital,
        strategyConfig: this.unavailableStrategySnapshot.strategyConfig,
        executionConfig: {
          makerFee: formValue.makerFee,
          takerFee: formValue.takerFee,
          slippage: formValue.slippage
        },
        enableAuditLog: formValue.enableAuditLog
      });

      return;
    }

    this.runBacktest.emit({
      strategyId: formValue.strategyId,
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
      initialCapital: formValue.initialCapital,
      executionConfig: {
        makerFee: formValue.makerFee,
        takerFee: formValue.takerFee,
        slippage: formValue.slippage
      },
      enableAuditLog: formValue.enableAuditLog
    });
  }

  public onValidateData(): void {
    this.submitted = true;
    this.form.controls.strategyId.markAsTouched();
    this.form.controls.startDate.markAsTouched();
    this.form.controls.endDate.markAsTouched();

    if (!this.canValidateCoverage) {
      return;
    }

    const formValue = this.form.getRawValue();
    const symbol = this.selectedStrategy?.config.market ?? this.unavailableStrategySnapshot?.symbol;

    if (symbol !== undefined) {
      this.validateData.emit({
        symbol,
        intervals: this.selectedIntervals,
        startDate: formValue.startDate!.toISOString(),
        endDate: formValue.endDate!.toISOString()
      });
    }
  }

  private _prefillFromResult(result: BacktestResult): void {
    this.form.patchValue({
      startDate: new Date(result.startDate),
      endDate: new Date(result.endDate),
      makerFee: result.executionConfig.feeModel.makerFeeRate,
      takerFee: result.executionConfig.feeModel.takerFeeRate,
      slippage: result.executionConfig.feeModel.slippageRate,
      initialCapital: result.initialCapital,
      enableAuditLog: result.hasAuditLog
    });

    this.unavailableStrategySnapshot = result.strategyId ? null : result;

    this._applyStrategyId(result.strategyId ?? null);
  }

  private _applyValidationError(message: string | null): void {
    this._clearValidationErrors();

    if (message === null || message.trim().length === 0) {
      return;
    }

    this.formLevelError = message;

    const lowerMessage = message.toLowerCase();

    if (lowerMessage.includes("enddate") && lowerMessage.includes("startdate")) {
      this._setFormError("serverDateRange");
      return;
    }

    const fieldKeywords: Record<Exclude<BacktestControlName, "enableAuditLog">, string[]> = {
      strategyId: ["strategyid", "strategy"],
      startDate: ["startdate"],
      endDate: ["enddate"],
      makerFee: ["makerfee"],
      takerFee: ["takerfee"],
      slippage: ["slippage"],
      initialCapital: ["initialcapital"]
    };

    const controlName = (Object.keys(fieldKeywords) as Exclude<BacktestControlName, "enableAuditLog">[])
      .find((candidate) => fieldKeywords[candidate].some((keyword) => lowerMessage.includes(keyword)));

    if (controlName !== undefined) {
      this._setControlError(controlName, "server");
    }
  }

  private _loadStrategies(): void {
    this.isLoadingStrategies = true;
    this.form.controls.strategyId.disable({ emitEvent: false });

    this._strategyApi.getStrategies().subscribe({
      next: (strategies: StrategySummaryDto[]) => {
        this.strategies = strategies;
        this.isLoadingStrategies = false;
        this.form.controls.strategyId.enable({ emitEvent: false });

        if (strategies.length === 0) {
          this.formLevelError = "Create a saved strategy before running a backtest.";
        }
      },
      error: () => {
        this.isLoadingStrategies = false;
        this.form.controls.strategyId.enable({ emitEvent: false });
        this.formLevelError = "Failed to load saved strategies.";
        this._notificationService.error("Failed to load saved strategies.");
      }
    });
  }

  private _applyStrategyId(strategyId: string | null): void {
    const normalizedValue = strategyId?.trim() ?? "";

    if (normalizedValue === this.form.controls.strategyId.value) {
      return;
    }

    this.form.controls.strategyId.setValue(normalizedValue);
  }

  private _setControlError(controlName: BacktestControlName, errorKey: string): void {
    const control = this.form.controls[controlName];
    control.setErrors({
      ...(control.errors ?? {}),
      [errorKey]: true
    });
  }

  private _setFormError(errorKey: string): void {
    this.form.setErrors({
      ...(this.form.errors ?? {}),
      [errorKey]: true
    });
  }

  private _clearValidationErrors(): void {
    this._removeError(this.form, "serverDateRange");

    (Object.keys(this.form.controls) as BacktestControlName[])
      .forEach((controlName) => this._removeError(this.form.controls[controlName], "server"));
  }

  private _removeError(control: AbstractControl, errorKey: string): void {
    if (control.errors === null || control.errors[errorKey] === undefined) {
      return;
    }

    const { [errorKey]: _removed, ...remainingErrors } = control.errors;
    void _removed;

    control.setErrors(Object.keys(remainingErrors).length > 0 ? remainingErrors : null);
  }
}
