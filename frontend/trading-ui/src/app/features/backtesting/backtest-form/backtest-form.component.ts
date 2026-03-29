import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatDividerModule } from "@angular/material/divider";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { BacktestRequest, BacktestResult } from "../../../core/models/backtest.model";

interface BacktestFormModel {
  symbol: FormControl<string>;
  startDate: FormControl<Date | null>;
  endDate: FormControl<Date | null>;
  interval15m: FormControl<boolean>;
  interval1h: FormControl<boolean>;
  interval4h: FormControl<boolean>;
  gridLevels: FormControl<number>;
  manualAnchorPrice: FormControl<number | null>;
  gridSpacing: FormControl<number>;
  takeProfitPercent: FormControl<number>;
  breakdownThreshold: FormControl<number>;
  makerFee: FormControl<number>;
  takerFee: FormControl<number>;
  slippage: FormControl<number>;
  positionSize: FormControl<number>;
  leverage: FormControl<number>;
  stopLossPercent: FormControl<number>;
  initialCapital: FormControl<number>;
}

export interface CoverageValidationRequest {
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
}

type BacktestControlName = keyof BacktestFormModel;

function dateRangeValidator(control: AbstractControl): ValidationErrors | null {
  const formGroup = control as FormGroup<BacktestFormModel>;
  const startDate = formGroup.controls.startDate.value;
  const endDate = formGroup.controls.endDate.value;

  if (startDate === null || endDate === null) {
    return null;
  }

  return startDate < endDate ? null : { dateRange: true };
}

function intervalSelectionValidator(control: AbstractControl): ValidationErrors | null {
  const formGroup = control as FormGroup<BacktestFormModel>;
  const hasSelection = formGroup.controls.interval15m.value ||
    formGroup.controls.interval1h.value ||
    formGroup.controls.interval4h.value;

  return hasSelection ? null : { intervals: true };
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
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: "./backtest-form.component.html",
  styleUrl: "./backtest-form.component.scss"
})
export class BacktestFormComponent implements OnChanges {
  private readonly _fb = inject(FormBuilder);
  private readonly _destroyRef = inject(DestroyRef);

  @Input()
  public isRunning = false;

  @Input()
  public isValidating = false;

  @Input()
  public prefillConfig: BacktestResult | null = null;

  @Input()
  public validationErrorMessage: string | null = null;

  @Output()
  public runBacktest = new EventEmitter<BacktestRequest>();

  @Output()
  public validateData = new EventEmitter<CoverageValidationRequest>();

  public readonly symbols = ["BTC", "ETH", "SOL", "DOGE", "ARB", "OP"];
  public readonly form: FormGroup<BacktestFormModel> = this._fb.group({
    symbol: this._fb.nonNullable.control("BTC"),
    startDate: this._fb.control<Date | null>(null, Validators.required),
    endDate: this._fb.control<Date | null>(null, Validators.required),
    interval15m: this._fb.nonNullable.control(true),
    interval1h: this._fb.nonNullable.control(true),
    interval4h: this._fb.nonNullable.control(true),
    gridLevels: this._fb.nonNullable.control(10, [Validators.required, Validators.min(1), Validators.max(50)]),
    manualAnchorPrice: this._fb.control<number | null>(null, [Validators.min(0.00000001)]),
    gridSpacing: this._fb.nonNullable.control(0.5, [Validators.required, Validators.min(0.001)]),
    takeProfitPercent: this._fb.nonNullable.control(1, [Validators.required, Validators.min(0.001)]),
    breakdownThreshold: this._fb.nonNullable.control(2, [Validators.required]),
    makerFee: this._fb.nonNullable.control(0.0001, [Validators.required, Validators.min(0)]),
    takerFee: this._fb.nonNullable.control(0.00035, [Validators.required, Validators.min(0)]),
    slippage: this._fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    positionSize: this._fb.nonNullable.control(100, [Validators.required, Validators.min(0.01)]),
    leverage: this._fb.nonNullable.control(3, [Validators.required, Validators.min(0.01), Validators.max(50)]),
    stopLossPercent: this._fb.nonNullable.control(5, [Validators.required, Validators.min(0.01)]),
    initialCapital: this._fb.nonNullable.control(10000, [Validators.required, Validators.min(100)])
  }, {
    validators: [dateRangeValidator, intervalSelectionValidator]
  });
  public submitted = false;
  public formLevelError: string | null = null;

  public constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._clearServerErrors();
      });
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["prefillConfig"] && this.prefillConfig !== null) {
      this._prefillFromResult(this.prefillConfig);
    }

    if (changes["validationErrorMessage"]) {
      this._applyValidationError(this.validationErrorMessage);
    }
  }

  public get isFormValid(): boolean {
    return this.form.valid;
  }

  public get canValidateCoverage(): boolean {
    return this.form.controls.startDate.valid &&
      this.form.controls.endDate.valid &&
      !this.form.hasError("dateRange") &&
      !this.form.hasError("intervals");
  }

  public hasControlError(controlName: BacktestControlName): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty || this.submitted);
  }

  public hasDateRangeError(): boolean {
    return (this.form.hasError("dateRange") || this.form.hasError("serverDateRange")) &&
      (this.form.controls.startDate.touched || this.form.controls.endDate.touched || this.submitted);
  }

  public hasIntervalError(): boolean {
    return (this.form.hasError("intervals") || this.form.hasError("serverIntervals")) && this.submitted;
  }

  public getControlErrorMessage(controlName: BacktestControlName): string {
    const errors = this.form.controls[controlName].errors;

    if (errors?.["server"]) {
      return this.validationErrorMessage ?? "Invalid value.";
    }

    if (errors?.["required"]) {
      return "This field is required.";
    }

    if (errors?.["min"]) {
      const requiredMin = errors["min"]["min"];
      return `Must be at least ${requiredMin}.`;
    }

    if (errors?.["max"]) {
      const requiredMax = errors["max"]["max"];
      return `Must be ${requiredMax} or less.`;
    }

    return "Invalid value.";
  }

  public getDateRangeErrorMessage(): string {
    if (this.form.hasError("serverDateRange")) {
      return this.validationErrorMessage ?? "End date must be after start date.";
    }

    return "End date must be after start date.";
  }

  public getIntervalErrorMessage(): string {
    if (this.form.hasError("serverIntervals")) {
      return this.validationErrorMessage ?? "Select at least one interval.";
    }

    return "Select at least one interval.";
  }

  public onRunBacktest(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (!this.isFormValid || this.isRunning) {
      return;
    }

    const formValue = this.form.getRawValue();

    this.runBacktest.emit({
      symbol: formValue.symbol,
      intervals: this.getSelectedIntervals(),
      startDate: formValue.startDate!.toISOString(),
      endDate: formValue.endDate!.toISOString(),
      initialCapital: formValue.initialCapital,
      strategyConfig: {
        gridLevels: formValue.gridLevels,
        manualAnchorPrice: formValue.manualAnchorPrice,
        gridSpacing: formValue.gridSpacing,
        takeProfitPercent: formValue.takeProfitPercent,
        breakdownThreshold: formValue.breakdownThreshold,
        makerFee: formValue.makerFee,
        takerFee: formValue.takerFee,
        slippage: formValue.slippage,
        positionSize: formValue.positionSize,
        leverage: formValue.leverage,
        stopLossPercent: formValue.stopLossPercent
      }
    });
  }

  public onValidateData(): void {
    this.submitted = true;
    this.form.controls.startDate.markAsTouched();
    this.form.controls.endDate.markAsTouched();

    if (!this.canValidateCoverage || this.isValidating) {
      return;
    }

    const formValue = this.form.getRawValue();

    this.validateData.emit({
      symbol: formValue.symbol,
      intervals: this.getSelectedIntervals(),
      startDate: formValue.startDate!.toISOString(),
      endDate: formValue.endDate!.toISOString()
    });
  }

  public getSelectedIntervals(): string[] {
    const formValue = this.form.getRawValue();
    const intervals: string[] = [];

    if (formValue.interval15m) {
      intervals.push("15m");
    }

    if (formValue.interval1h) {
      intervals.push("1h");
    }

    if (formValue.interval4h) {
      intervals.push("4h");
    }

    return intervals;
  }

  private _prefillFromResult(result: BacktestResult): void {
    this.form.patchValue({
      symbol: result.symbol,
      startDate: new Date(result.startDate),
      endDate: new Date(result.endDate),
      interval15m: result.intervals.includes("15m"),
      interval1h: result.intervals.includes("1h"),
      interval4h: result.intervals.includes("4h"),
      gridLevels: result.strategyConfig.gridLevels,
      manualAnchorPrice: result.strategyConfig.manualAnchorPrice ?? null,
      gridSpacing: result.strategyConfig.gridSpacing,
      takeProfitPercent: result.strategyConfig.takeProfitPercent,
      breakdownThreshold: result.strategyConfig.breakdownThreshold,
      makerFee: result.strategyConfig.makerFee,
      takerFee: result.strategyConfig.takerFee,
      slippage: result.strategyConfig.slippage,
      positionSize: result.strategyConfig.positionSize,
      leverage: result.strategyConfig.leverage,
      stopLossPercent: result.strategyConfig.stopLossPercent,
      initialCapital: result.initialCapital
    });
  }

  private _applyValidationError(message: string | null): void {
    this._clearServerErrors();

    if (message === null || message.trim().length === 0) {
      return;
    }

    const lowerMessage = message.toLowerCase();

    if (lowerMessage.includes("enddate") || lowerMessage.includes("startdate")) {
      this._setFormError("serverDateRange");
      return;
    }

    if (lowerMessage.includes("interval")) {
      this._setFormError("serverIntervals");
      return;
    }

    const controlMap: Record<BacktestControlName, string[]> = {
      symbol: ["symbol"],
      startDate: ["startdate"],
      endDate: ["enddate"],
      interval15m: ["15m"],
      interval1h: ["1h"],
      interval4h: ["4h"],
      gridLevels: ["gridlevels"],
      manualAnchorPrice: ["manualanchorprice", "anchorprice"],
      gridSpacing: ["gridspacing"],
      takeProfitPercent: ["takeprofitpercent"],
      breakdownThreshold: ["breakdownthreshold"],
      makerFee: ["makerfee"],
      takerFee: ["takerfee"],
      slippage: ["slippage"],
      positionSize: ["positionsize"],
      leverage: ["leverage"],
      stopLossPercent: ["stoplosspercent"],
      initialCapital: ["initialcapital"]
    };

    const matchingControl = (Object.keys(controlMap) as BacktestControlName[])
      .find((controlName) => controlMap[controlName].some((token) => lowerMessage.includes(token)));

    if (matchingControl !== undefined) {
      this._setControlServerError(matchingControl);
      return;
    }

    this.formLevelError = message;
    this._setFormError("server");
  }

  private _setControlServerError(controlName: BacktestControlName): void {
    const control = this.form.controls[controlName];
    control.setErrors({
      ...(control.errors ?? {}),
      server: true
    });
  }

  private _setFormError(errorKey: string): void {
    this.form.setErrors({
      ...(this.form.errors ?? {}),
      [errorKey]: true
    });
  }

  private _clearServerErrors(): void {
    this.formLevelError = null;

    for (const control of Object.values(this.form.controls)) {
      this._removeError(control, "server");
    }

    this._removeError(this.form, "serverDateRange");
    this._removeError(this.form, "serverIntervals");
    this._removeError(this.form, "server");
  }

  private _removeError(control: AbstractControl, errorKey: string): void {
    if (!control.errors?.[errorKey]) {
      return;
    }

    const remainingErrors = { ...control.errors };
    delete remainingErrors[errorKey];
    control.setErrors(Object.keys(remainingErrors).length > 0 ? remainingErrors : null);
  }
}