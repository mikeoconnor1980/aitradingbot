import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject } from "@angular/core";
import { DecimalPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatChipsModule } from "@angular/material/chips";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatRadioModule } from "@angular/material/radio";
import { MatSelectModule } from "@angular/material/select";
import { MatSliderModule } from "@angular/material/slider";
import { MatTooltipModule } from "@angular/material/tooltip";
import { RunOptimizationRequest, SweepConfigSnapshot } from "../../../core/models/optimizer.model";

interface OptimizerConfigFormModel {
  symbol: FormControl<string>;
  startDate: FormControl<Date | null>;
  endDate: FormControl<Date | null>;
  initialCapital: FormControl<number>;
  sampleSize: FormControl<number>;
  timeframes: FormControl<string[]>;
  stopLossMin: FormControl<number>;
  stopLossMax: FormControl<number>;
  takeProfitMin: FormControl<number>;
  takeProfitMax: FormControl<number>;
  leverage: FormControl<number>;
  positionSizePercent: FormControl<number>;
  direction: FormControl<string>;
  rsiPeriods: FormControl<number[]>;
  rsiThresholds: FormControl<number[]>;
  rsiLt: FormControl<boolean>;
  rsiGt: FormControl<boolean>;
  rsiCrossAbove: FormControl<boolean>;
  rsiCrossBelow: FormControl<boolean>;
  macdFastPeriods: FormControl<number[]>;
  macdSlowPeriods: FormControl<number[]>;
  macdCrossAboveSignal: FormControl<boolean>;
  macdCrossBelowSignal: FormControl<boolean>;
  macdAboveZero: FormControl<boolean>;
  macdHistogramRising: FormControl<boolean>;
  emaPeriods: FormControl<number[]>;
  emaProximityPercents: FormControl<number[]>;
  emaNear: FormControl<boolean>;
  emaAbove: FormControl<boolean>;
  emaBelow: FormControl<boolean>;
  emaCrossAbove: FormControl<boolean>;
  exitOnOppositeSignal: FormControl<boolean>;
  includeTrendFilter: FormControl<boolean>;
  minWinRate: FormControl<number>;
  minTotalTrades: FormControl<number>;
  maxDrawdownPercent: FormControl<number>;
  walkForwardEnabled: FormControl<boolean>;
  walkForwardSplitPercent: FormControl<number>;
  evolutionaryEnabled: FormControl<boolean>;
  evolutionaryGenerations: FormControl<number>;
  evolutionaryEliteCount: FormControl<number>;
  evolutionaryMutationRate: FormControl<number>;
  evolutionaryCrossoverRate: FormControl<number>;
}

export interface OperatorOption {
  key: string;
  label: string;
  controlName: keyof OptimizerConfigFormModel;
  directions: string[];
}

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
  const formGroup = control as FormGroup<OptimizerConfigFormModel>;
  const startDate = formGroup.controls.startDate.value;
  const endDate = formGroup.controls.endDate.value;

  if (startDate === null || endDate === null) {
    return null;
  }

  return startDate < endDate ? null : { dateRange: true };
}

@Component({
  selector: "app-optimizer-config-form",
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatRadioModule,
    MatSelectModule,
    MatSliderModule,
    MatTooltipModule
  ],
  templateUrl: "./optimizer-config-form.component.html",
  styleUrl: "./optimizer-config-form.component.scss"
})
export class OptimizerConfigFormComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  @Input()
  public isRunning = false;

  @Output()
  public runOptimization = new EventEmitter<RunOptimizationRequest>();

  public submitted = false;
  public readonly maxSelectableDate = normalizeDateOnly(new Date());

  public get filteredRsiOptions(): OperatorOption[] {
    return this.rsiOperatorOptions.filter(o => o.directions.includes(this.form.controls.direction.value));
  }

  public get filteredMacdOptions(): OperatorOption[] {
    return this.macdOperatorOptions.filter(o => o.directions.includes(this.form.controls.direction.value));
  }

  public get filteredEmaOptions(): OperatorOption[] {
    return this.emaOperatorOptions.filter(o => o.directions.includes(this.form.controls.direction.value));
  }

  public readonly availableTimeframes = [
    { value: "1m", label: "1m" },
    { value: "3m", label: "3m" },
    { value: "5m", label: "5m" },
    { value: "15m", label: "15m" },
    { value: "30m", label: "30m" },
    { value: "1h", label: "1h" },
    { value: "2h", label: "2h" },
    { value: "4h", label: "4h" },
    { value: "6h", label: "6h" },
    { value: "12h", label: "12h" },
    { value: "1d", label: "1d" }
  ];

  public readonly rsiOperatorOptions: OperatorOption[] = [
    { key: "lt", label: "Oversold (RSI < threshold)", controlName: "rsiLt", directions: ["Long"] },
    { key: "gt", label: "Overbought (RSI > threshold)", controlName: "rsiGt", directions: ["Short"] },
    { key: "cross_above", label: "Cross Above threshold", controlName: "rsiCrossAbove", directions: ["Long"] },
    { key: "cross_below", label: "Cross Below threshold", controlName: "rsiCrossBelow", directions: ["Short"] }
  ];

  public readonly availableRsiPeriods = [7, 14, 21];
  public readonly availableRsiThresholds = [30, 35, 40, 45];

  public readonly macdOperatorOptions: OperatorOption[] = [
    { key: "cross_above_signal", label: "Bullish Cross (MACD crosses above signal)", controlName: "macdCrossAboveSignal", directions: ["Long"] },
    { key: "cross_below_signal", label: "Bearish Cross (MACD crosses below signal)", controlName: "macdCrossBelowSignal", directions: ["Short"] },
    { key: "above_zero", label: "Above Zero Line", controlName: "macdAboveZero", directions: ["Long"] },
    { key: "histogram_rising", label: "Histogram Rising", controlName: "macdHistogramRising", directions: ["Long", "Short"] }
  ];

  public readonly availableMacdFastPeriods = [8, 12, 16];
  public readonly availableMacdSlowPeriods = [21, 26, 30];

  public readonly emaOperatorOptions: OperatorOption[] = [
    { key: "near", label: "Price Near EMA", controlName: "emaNear", directions: ["Long", "Short"] },
    { key: "above", label: "Price Above EMA", controlName: "emaAbove", directions: ["Long"] },
    { key: "below", label: "Price Below EMA", controlName: "emaBelow", directions: ["Short"] },
    { key: "cross_above", label: "Price Cross Above EMA", controlName: "emaCrossAbove", directions: ["Long"] }
  ];

  public readonly availableEmaPeriods = [20, 50, 100];
  public readonly availableEmaProximity = [0.15, 0.25, 0.5];

  public readonly form = new FormGroup<OptimizerConfigFormModel>({
    symbol: new FormControl<string>("BTC", { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl<Date | null>(new Date(new Date().setMonth(new Date().getMonth() - 3)), { validators: [Validators.required, futureDateValidator] }),
    endDate: new FormControl<Date | null>(new Date(), { validators: [Validators.required, futureDateValidator] }),
    initialCapital: new FormControl<number>(10000, { nonNullable: true, validators: [Validators.required, Validators.min(100)] }),
    sampleSize: new FormControl<number>(500, { nonNullable: true, validators: [Validators.required, Validators.min(10)] }),
    timeframes: new FormControl<string[]>(["15m"], { nonNullable: true, validators: [Validators.required, Validators.minLength(1)] }),
    stopLossMin: new FormControl<number>(1, { nonNullable: true }),
    stopLossMax: new FormControl<number>(5, { nonNullable: true }),
    takeProfitMin: new FormControl<number>(1, { nonNullable: true }),
    takeProfitMax: new FormControl<number>(6, { nonNullable: true }),
    leverage: new FormControl<number>(5, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(50)] }),
    positionSizePercent: new FormControl<number>(15, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(100)] }),
    direction: new FormControl<string>("Short", { nonNullable: true, validators: [Validators.required] }),
    rsiPeriods: new FormControl<number[]>([7, 14, 21], { nonNullable: true }),
    rsiThresholds: new FormControl<number[]>([30, 35, 40, 45], { nonNullable: true }),
    rsiLt: new FormControl<boolean>(true, { nonNullable: true }),
    rsiGt: new FormControl<boolean>(true, { nonNullable: true }),
    rsiCrossAbove: new FormControl<boolean>(true, { nonNullable: true }),
    rsiCrossBelow: new FormControl<boolean>(true, { nonNullable: true }),
    macdFastPeriods: new FormControl<number[]>([8, 12, 16], { nonNullable: true }),
    macdSlowPeriods: new FormControl<number[]>([21, 26, 30], { nonNullable: true }),
    macdCrossAboveSignal: new FormControl<boolean>(true, { nonNullable: true }),
    macdCrossBelowSignal: new FormControl<boolean>(true, { nonNullable: true }),
    macdAboveZero: new FormControl<boolean>(true, { nonNullable: true }),
    macdHistogramRising: new FormControl<boolean>(true, { nonNullable: true }),
    emaPeriods: new FormControl<number[]>([20, 50, 100], { nonNullable: true }),
    emaProximityPercents: new FormControl<number[]>([0.15, 0.25, 0.5], { nonNullable: true }),
    emaNear: new FormControl<boolean>(true, { nonNullable: true }),
    emaAbove: new FormControl<boolean>(true, { nonNullable: true }),
    emaBelow: new FormControl<boolean>(true, { nonNullable: true }),
    emaCrossAbove: new FormControl<boolean>(true, { nonNullable: true }),
    exitOnOppositeSignal: new FormControl<boolean>(false, { nonNullable: true }),
    includeTrendFilter: new FormControl<boolean>(true, { nonNullable: true }),
    minWinRate: new FormControl<number>(40, { nonNullable: true, validators: [Validators.required, Validators.min(0), Validators.max(100)] }),
    minTotalTrades: new FormControl<number>(10, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    maxDrawdownPercent: new FormControl<number>(30, { nonNullable: true, validators: [Validators.required, Validators.min(0.1), Validators.max(100)] }),
    walkForwardEnabled: new FormControl<boolean>(false, { nonNullable: true }),
    walkForwardSplitPercent: new FormControl<number>(30, { nonNullable: true, validators: [Validators.min(5), Validators.max(50)] }),
    evolutionaryEnabled: new FormControl<boolean>(false, { nonNullable: true }),
    evolutionaryGenerations: new FormControl<number>(5, { nonNullable: true, validators: [Validators.min(1), Validators.max(20)] }),
    evolutionaryEliteCount: new FormControl<number>(10, { nonNullable: true, validators: [Validators.min(2), Validators.max(50)] }),
    evolutionaryMutationRate: new FormControl<number>(30, { nonNullable: true, validators: [Validators.min(0), Validators.max(100)] }),
    evolutionaryCrossoverRate: new FormControl<number>(70, { nonNullable: true, validators: [Validators.min(0), Validators.max(100)] })
  }, {
    validators: [dateRangeValidator]
  });

  public ngOnInit(): void {
    this.applyDirectionDefaults(this.form.controls.direction.value);

    this.form.controls.direction.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(direction => this.applyDirectionDefaults(direction));
  }

  public onSubmit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    const startDate = this.form.controls.startDate.value;
    const endDate = this.form.controls.endDate.value;

    if (startDate === null || endDate === null) {
      return;
    }

    this.runOptimization.emit({
      symbol: this.form.controls.symbol.value.trim().toUpperCase(),
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
      initialCapital: this.form.controls.initialCapital.value,
      sampleSize: this.form.controls.sampleSize.value,
      directions: [this.form.controls.direction.value],
      timeframes: this.form.controls.timeframes.value.length > 0 ? this.form.controls.timeframes.value : null,
      stopLossMin: this.form.controls.stopLossMin.value,
      stopLossMax: this.form.controls.stopLossMax.value,
      takeProfitMin: this.form.controls.takeProfitMin.value,
      takeProfitMax: this.form.controls.takeProfitMax.value,
      leverageMin: this.form.controls.leverage.value,
      leverageMax: this.form.controls.leverage.value,
      positionSizePercent: this.form.controls.positionSizePercent.value,
      rsiOperators: this.getSelectedOperators(this.rsiOperatorOptions),
      rsiPeriods: this.form.controls.rsiPeriods.value,
      rsiThresholds: this.form.controls.rsiThresholds.value,
      macdOperators: this.getSelectedOperators(this.macdOperatorOptions),
      macdFastPeriods: this.form.controls.macdFastPeriods.value,
      macdSlowPeriods: this.form.controls.macdSlowPeriods.value,
      priceVsEmaOperators: this.getSelectedOperators(this.emaOperatorOptions),
      emaPeriods: this.form.controls.emaPeriods.value,
      emaProximityPercents: this.form.controls.emaProximityPercents.value,
      exitOnOppositeSignal: this.form.controls.exitOnOppositeSignal.value ? true : null,
      includeTrendFilter: this.form.controls.includeTrendFilter.value,
      minWinRate: this.form.controls.minWinRate.value,
      minTotalTrades: this.form.controls.minTotalTrades.value,
      maxDrawdownPercent: this.form.controls.maxDrawdownPercent.value,
      walkForwardEnabled: this.form.controls.walkForwardEnabled.value || null,
      walkForwardSplitPercent: this.form.controls.walkForwardEnabled.value ? this.form.controls.walkForwardSplitPercent.value : null,
      evolutionaryEnabled: this.form.controls.evolutionaryEnabled.value || null,
      evolutionaryGenerations: this.form.controls.evolutionaryEnabled.value ? this.form.controls.evolutionaryGenerations.value : null,
      evolutionaryEliteCount: this.form.controls.evolutionaryEnabled.value ? this.form.controls.evolutionaryEliteCount.value : null,
      evolutionaryMutationRate: this.form.controls.evolutionaryEnabled.value ? this.form.controls.evolutionaryMutationRate.value / 100 : null,
      evolutionaryCrossoverRate: this.form.controls.evolutionaryEnabled.value ? this.form.controls.evolutionaryCrossoverRate.value / 100 : null,
    });
  }

  public hasControlError(name: keyof OptimizerConfigFormModel): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted);
  }

  public getControlErrorMessage(name: keyof OptimizerConfigFormModel): string {
    const control = this.form.controls[name];

    if (control.hasError("required")) {
      return "This field is required.";
    }

    if (control.hasError("futureDate")) {
      return "Date cannot be in the future.";
    }

    if (control.hasError("min")) {
      return "Value is below the allowed minimum.";
    }

    if (control.hasError("max")) {
      return "Value exceeds the allowed maximum.";
    }

    return "Invalid value.";
  }

  public get formErrorMessage(): string | null {
    if (this.form.hasError("dateRange")) {
      return "End date must be after the start date.";
    }

    return null;
  }

  public formatSliderLabel(value: number): string {
    return `${value}`;
  }

  public get estimatedCombinations(): number {
    const f = this.form.controls;

    // Stop Loss: (max - min) / 0.5 + 1
    const slSteps = Math.max(1, Math.floor((f.stopLossMax.value - f.stopLossMin.value) / 0.5) + 1);
    // Take Profit: (max - min) / 1 + 1
    const tpSteps = Math.max(1, Math.floor((f.takeProfitMax.value - f.takeProfitMin.value) / 1) + 1);
    // Timeframes
    const timeframeCount = Math.max(1, f.timeframes.value.length);
    // Signal operators
    const rsiOps = this.getSelectedOperators(this.rsiOperatorOptions).length;
    const macdOps = this.getSelectedOperators(this.macdOperatorOptions).length;
    const emaOps = this.getSelectedOperators(this.emaOperatorOptions).length;
    // RSI: periods × thresholds × ops
    const rsiCombos = rsiOps > 0 ? Math.max(1, f.rsiPeriods.value.length) * Math.max(1, f.rsiThresholds.value.length) * rsiOps : 0;
    // MACD: fast × slow × ops
    const macdCombos = macdOps > 0 ? Math.max(1, f.macdFastPeriods.value.length) * Math.max(1, f.macdSlowPeriods.value.length) * macdOps : 0;
    // EMA: periods × proximities × ops
    const emaCombos = emaOps > 0 ? Math.max(1, f.emaPeriods.value.length) * Math.max(1, f.emaProximityPercents.value.length) * emaOps : 0;
    // Signal templates that use each type (of 11 total)
    // Single: RSI(1), MACD(1), EMA(1)
    // Pairs with All/Any: RSI+MACD(2), RSI+EMA(2), MACD+EMA(2)
    // Triple All/Any: R+M+E(2) — but each condition is independent
    // Simpler: total signal combos ≈ RSI + MACD + EMA (each template picks randomly)
    const signalCombos = Math.max(1, rsiCombos + macdCombos + emaCombos);
    // Exit options
    const exitOpts = f.exitOnOppositeSignal.value ? 1 : 2;
    // MaxOpenTrades(3) × Cooldown(4) fixed
    const riskCombos = 3 * 4;
    // Trend filter: roughly 2× if enabled (with/without)
    const trendMultiplier = f.includeTrendFilter.value ? 2 : 1;

    return timeframeCount * signalCombos * slSteps * tpSteps * exitOpts * riskCombos * trendMultiplier;
  }

  private getSelectedOperators(options: OperatorOption[]): string[] {
    return options
      .filter(opt => this.form.controls[opt.controlName].value === true)
      .map(opt => opt.key);
  }

  private applyDirectionDefaults(direction: string): void {
    const allOptions = [...this.rsiOperatorOptions, ...this.macdOperatorOptions, ...this.emaOperatorOptions];

    for (const opt of allOptions) {
      const control = this.form.controls[opt.controlName] as FormControl<boolean>;
      control.setValue(opt.directions.includes(direction));
    }
  }

  public prefill(config: SweepConfigSnapshot): void {
    const b = config.Bounds;
    const t = config.Thresholds;

    this.form.patchValue({
      symbol: config.Symbol,
      startDate: new Date(config.StartDateUtc),
      endDate: new Date(config.EndDateUtc),
      initialCapital: config.InitialCapital,
      sampleSize: config.SampleSize,
      timeframes: b.Timeframes ?? ["15m"],
      stopLossMin: b.StopLossMin,
      stopLossMax: b.StopLossMax,
      takeProfitMin: b.TakeProfitMin,
      takeProfitMax: b.TakeProfitMax,
      leverage: b.LeverageMin,
      positionSizePercent: b.PositionSizeOptions?.[0] ?? 15,
      direction: b.Directions.includes(0) ? "Long" : "Short",
      rsiPeriods: b.RsiPeriods ?? [7, 14, 21],
      rsiThresholds: b.RsiThresholds ?? [30, 35, 40, 45],
      rsiLt: b.RsiOperators.includes("lt"),
      rsiGt: b.RsiOperators.includes("gt"),
      rsiCrossAbove: b.RsiOperators.includes("cross_above"),
      rsiCrossBelow: b.RsiOperators.includes("cross_below"),
      macdFastPeriods: b.MacdFastPeriods ?? [8, 12, 16],
      macdSlowPeriods: b.MacdSlowPeriods ?? [21, 26, 30],
      macdCrossAboveSignal: b.MacdOperators.includes("cross_above_signal"),
      macdCrossBelowSignal: b.MacdOperators.includes("cross_below_signal"),
      macdAboveZero: b.MacdOperators.includes("above_zero"),
      macdHistogramRising: b.MacdOperators.includes("histogram_rising"),
      emaPeriods: b.EmaPeriods ?? [20, 50, 100],
      emaProximityPercents: b.EmaProximityPercents ?? [0.15, 0.25, 0.5],
      emaNear: b.PriceVsEmaOperators.includes("near"),
      emaAbove: b.PriceVsEmaOperators.includes("above"),
      emaBelow: b.PriceVsEmaOperators.includes("below"),
      emaCrossAbove: b.PriceVsEmaOperators.includes("cross_above"),
      exitOnOppositeSignal: b.ExitOnOppositeSignalOptions.includes(true) && !b.ExitOnOppositeSignalOptions.includes(false),
      includeTrendFilter: b.IncludeTrendFilter,
      minWinRate: t.MinWinRate,
      minTotalTrades: t.MinTotalTrades,
      maxDrawdownPercent: t.MaxDrawdownPercent,
    });

    if (config.WalkForward) {
      this.form.patchValue({
        walkForwardEnabled: config.WalkForward.Enabled,
        walkForwardSplitPercent: config.WalkForward.SplitPercent ?? 30,
      });
    }

    if (config.Evolutionary) {
      this.form.patchValue({
        evolutionaryEnabled: config.Evolutionary.Enabled,
        evolutionaryGenerations: config.Evolutionary.Generations ?? 5,
        evolutionaryEliteCount: config.Evolutionary.EliteCount ?? 10,
        evolutionaryMutationRate: config.Evolutionary.MutationRate != null ? config.Evolutionary.MutationRate * 100 : 30,
        evolutionaryCrossoverRate: config.Evolutionary.CrossoverRate != null ? config.Evolutionary.CrossoverRate * 100 : 70,
      });
    }

    this.submitted = false;
  }
}