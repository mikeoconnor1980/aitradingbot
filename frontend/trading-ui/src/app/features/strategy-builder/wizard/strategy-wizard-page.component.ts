import { HttpContext } from "@angular/common/http";
import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatStepper, MatStepperModule } from "@angular/material/stepper";
import { Router } from "@angular/router";
import { STEPPER_GLOBAL_OPTIONS } from "@angular/cdk/stepper";
import { debounceTime, map, startWith, switchMap, tap } from "rxjs";
import { of } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { NotificationFacade } from "../../../core/services/notification-facade.service";
import { StrategyConfig, ServerValidationResult, ValidationError } from "../models/strategy.model";
import { StrategyApiService } from "../services/strategy-api.service";
import { StrategyMapperService } from "../services/strategy-mapper.service";
import { StrategyValidationService } from "../services/strategy-validation.service";
import { ConditionFactoryService } from "../services/condition-factory.service";
import { StrategyDraftService } from "../services/strategy-draft.service";
import { WizardEducationService, WizardStepEducation } from "./services/wizard-education.service";
import { WizardGoalStepComponent } from "./steps/wizard-goal-step/wizard-goal-step.component";
import { WizardMarketStepComponent } from "./steps/wizard-market-step/wizard-market-step.component";
import { WizardEntryStepComponent } from "./steps/wizard-entry-step/wizard-entry-step.component";
import { WizardExitStepComponent } from "./steps/wizard-exit-step/wizard-exit-step.component";
import { WizardRiskStepComponent } from "./steps/wizard-risk-step/wizard-risk-step.component";
import { WizardFilterStepComponent } from "./steps/wizard-filter-step/wizard-filter-step.component";
import { WizardReviewStepComponent } from "./steps/wizard-review-step/wizard-review-step.component";

@Component({
  selector: "app-strategy-wizard-page",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatStepperModule,
    WizardGoalStepComponent,
    WizardMarketStepComponent,
    WizardEntryStepComponent,
    WizardExitStepComponent,
    WizardRiskStepComponent,
    WizardFilterStepComponent,
    WizardReviewStepComponent,
  ],
  providers: [
    { provide: STEPPER_GLOBAL_OPTIONS, useValue: { showError: true } }
  ],
  templateUrl: "./strategy-wizard-page.component.html",
  styleUrl: "./strategy-wizard-page.component.scss"
})
export class StrategyWizardPageComponent implements OnInit {
  private readonly _fb = inject(FormBuilder);
  private readonly _router = inject(Router);
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _strategyMapper = inject(StrategyMapperService);
  private readonly _strategyValidator = inject(StrategyValidationService);
  private readonly _draftService = inject(StrategyDraftService);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _conditionFactory = inject(ConditionFactoryService);
  public readonly _education = inject(WizardEducationService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @ViewChild("stepper") public stepper!: MatStepper;

  private readonly _signalTemplates = new Set(["custom_signal", "ema_pullback", "macd_cross", "rsi_reversal", "blank"]);

  public form: FormGroup = this._buildForm();
  public entryStepForm: FormGroup = this._fb.group({}, { validators: [this._entryStepValidator()] });
  public isSaving = false;
  public clientErrors: ValidationError[] = [];
  public serverErrors: ValidationError[] = [];
  public serverWarnings: ValidationError[] = [];
  public currentStepIndex = 0;
  private _userEditedName = false;

  public get selectedTemplateId(): string {
    return String(this.form.get("templateId")?.value ?? "grid");
  }

  public get selectedTemplateLabel(): string {
    const template = this._education.templates.find((t) => t.id === this.selectedTemplateId);

    return template?.label ?? "Custom";
  }

  public get isSignalMode(): boolean {
    return this._signalTemplates.has(this.selectedTemplateId);
  }

  public get showFilterStep(): boolean {
    return this.isSignalMode;
  }

  public get currentStepEducation(): WizardStepEducation {
    return this._education.getStepEducation(this.currentStepIndex);
  }

  public get marketGroup(): FormGroup {
    return this.form;
  }

  public get gridGroup(): FormGroup {
    return this.form.get("grid") as FormGroup;
  }

  public get exitGroup(): FormGroup {
    return this.form.get("exit") as FormGroup;
  }

  public get riskGroup(): FormGroup {
    return this.form.get("risk") as FormGroup;
  }

  public get trendFilterGroup(): FormGroup {
    return this.form.get("trendFilter") as FormGroup;
  }

  public get conditionsArray(): FormArray {
    return this.form.get("conditions") as FormArray;
  }

  public get entryLogicControl() {
    return this.form.get("entryLogic")!;
  }

  public get entryStepErrorMessage(): string {
    if (this.isSignalMode) {
      return "At least one entry condition required";
    }

    return "Grid configuration is incomplete";
  }

  public get currentConfig(): StrategyConfig {
    return this._strategyMapper.mapFormToConfig(this.form.getRawValue() as Record<string, unknown>);
  }

  public get allErrors(): ValidationError[] {
    return this.clientErrors.concat(this.serverErrors);
  }

  public ngOnInit(): void {
    const draft = this._draftService.draft;

    if (draft !== null) {
      this._populateFormFromDraft(draft);
    }

    this._setupValidationStream();
    this._setupEntryStepValidation();
    this._setupAutoSaveDraft();
    this._setupAutoName();
  }

  public onTemplateSelected(templateId: string): void {
    this.form.patchValue({ templateId });

    if (this._signalTemplates.has(templateId)) {
      this.form.get("grid")?.disable();
    } else {
      this.form.get("grid")?.enable();
    }

    this._applyTemplateDefaults(templateId);
    this._maybeAutoName();
    this.entryStepForm.updateValueAndValidity();
  }

  public onStepChange(index: number): void {
    this.currentStepIndex = index;
    this._persistDraft();
  }

  public onSave(): void {
    this.form.markAllAsTouched();

    if (this.allErrors.length > 0) {
      return;
    }

    this.isSaving = true;
    const config = this.currentConfig;
    config.source = {
      entryPoint: "ui_wizard",
      summary: "Created via strategy wizard"
    };

    this._strategyApi.createStrategy(config, this._localErrorContext).subscribe({
      next: (result) => {
        this.isSaving = false;
        this._draftService.clear();
        this._notifications.success("Strategy created successfully");
        void this._router.navigate(["/strategies", result.id, "edit"]);
      },
      error: () => {
        this.isSaving = false;
        this._notifications.error("Failed to create strategy.");
      }
    });
  }

  public onSwitchToBuilder(): void {
    this._persistDraft();
    const config = this.currentConfig;

    void this._router.navigate(["/strategies/new"], {
      state: { prefillConfig: config }
    });
  }

  public onCancel(): void {
    this._draftService.clear();
    void this._router.navigate(["/strategies"]);
  }

  private _buildForm(): FormGroup {
    return this._fb.group({
      templateId: ["grid"],
      strategyName: ["", [Validators.required, Validators.maxLength(100)]],
      exchange: ["Hyperliquid", Validators.required],
      market: ["BTC-USD", Validators.required],
      timeframe: ["15m", Validators.required],
      direction: ["long", Validators.required],
      grid: this._fb.group({
        levels: [10, [Validators.required, Validators.min(1), Validators.max(50)]],
        spacing: [0.5, [Validators.required, Validators.min(0.01), Validators.max(10)]],
        entryMode: ["auto_from_signal_candle", Validators.required],
        anchorPrice: [null],
        breakdownThreshold: [1.5, [Validators.required, Validators.min(0), Validators.max(10)]],
      }),
      exit: this._fb.group({
        takeProfit: this._fb.group({
          enabled: [true],
          type: ["fixed_percent", Validators.required],
          value: [2, [Validators.min(0.01), Validators.max(50)]],
        }),
        stopLoss: this._fb.group({
          enabled: [true],
          type: ["fixed_percent"],
          value: [6, [Validators.min(0.01), Validators.max(50)]],
          lookback: [null, [Validators.min(1)]],
          atrMultiplier: [3, [Validators.min(0.1), Validators.max(10)]],
          trailingStopWarmup: [3, [Validators.min(0), Validators.max(20)]],
        }),
        exitOnOppositeSignal: [false],
      }),
      risk: this._fb.group({
        positionSizeType: ["percent_wallet", Validators.required],
        positionSizeValue: [5, [Validators.required, Validators.min(0.01), Validators.max(100)]],
        leverage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
        maxOpenTrades: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
        cooldownValue: [0, [Validators.min(0)]],
        cooldownUnit: ["candles", Validators.required],
        allowSameCandleReentry: [false],
        riskPerTradePercent: [1, [Validators.min(0.01), Validators.max(100)]],
        autoLeverage: [true],
      }),
      metadata: this._fb.group({
        tags: [[]],
        notes: [""],
      }),
      source: this._fb.group({
        entryPoint: ["ui_wizard"],
        summary: ["Created via strategy wizard"],
        sourceText: [null],
      }),
      trendFilter: this._fb.group({
        enabled: [false],
        type: ["ema_cross", Validators.required],
        period: [200, [Validators.min(1)]],
        fastPeriod: [50, [Validators.required, Validators.min(1)]],
        slowPeriod: [200, [Validators.required, Validators.min(1)]],
        operator: ["gt", Validators.required],
        appliesTo: ["both", Validators.required],
      }),
      entryLogic: ["all"],
      conditions: this._fb.array([]),
    });
  }

  private _setupValidationStream(): void {
    this.form.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef),
      startWith(null),
      debounceTime(400),
      map(() => this.form.getRawValue() as Record<string, unknown>),
      tap((rawValue) => {
        this.clientErrors = this._strategyValidator.validate(rawValue);
      }),
      map((rawValue) => {
        if (this.clientErrors.length > 0 || this.form.invalid) {
          return null;
        }

        return this._strategyMapper.mapFormToConfig(rawValue);
      }),
      switchMap((config) => {
        if (config === null) {
          return of<ServerValidationResult | null>(null);
        }

        return this._strategyApi.validateStrategy(config, this._localErrorContext).pipe(
          map((result) => result as ServerValidationResult | null)
        );
      })
    ).subscribe((result) => {
      if (result !== null) {
        this.serverErrors = result.errors;
        this.serverWarnings = result.warnings;
      } else {
        this.serverErrors = [];
        this.serverWarnings = [];
      }
    });
  }

  private _setupAutoSaveDraft(): void {
    this.form.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef),
      debounceTime(1000)
    ).subscribe(() => this._persistDraft());
  }

  private _setupAutoName(): void {
    const nameControl = this.form.get("strategyName")!;

    nameControl.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe((value: string) => {
      const generated = this._generateName();

      if (value && value !== generated) {
        this._userEditedName = true;
      }
    });

    this.form.get("templateId")!.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this._maybeAutoName());

    this.form.get("market")!.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this._maybeAutoName());

    this.form.get("timeframe")!.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this._maybeAutoName());

    this.form.get("direction")!.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this._maybeAutoName());
  }

  private _maybeAutoName(): void {
    if (this._userEditedName) {
      return;
    }

    const name = this._generateName();
    this.form.get("strategyName")!.setValue(name, { emitEvent: false });
  }

  private _generateName(): string {
    const templateLabels: Record<string, string> = {
      grid: "Grid",
      custom_signal: "Signal",
      ema_pullback: "EMA Pullback",
      macd_cross: "MACD Cross",
      rsi_reversal: "RSI Reversal",
      blank: "Custom",
    };

    const templateId = String(this.form.get("templateId")?.value ?? "grid");
    const market = String(this.form.get("market")?.value ?? "BTC-USD");
    const timeframe = String(this.form.get("timeframe")?.value ?? "15m");
    const direction = String(this.form.get("direction")?.value ?? "long");
    const templateLabel = templateLabels[templateId] ?? "Strategy";
    const dirLabel = direction === "both" ? "L/S" : direction.charAt(0).toUpperCase() + direction.slice(1);

    return `${templateLabel} ${market} ${timeframe} ${dirLabel}`;
  }

  private _applyTemplateDefaults(templateId: string): void {
    switch (templateId) {
      case "ema_pullback":
        this._applyEmaPullbackDefaults();
        break;
      case "macd_cross":
        this._applyMacdCrossDefaults();
        break;
      case "custom_signal":
      case "blank":
        this.conditionsArray.clear();
        break;
      case "grid":
        this.conditionsArray.clear();
        this._resetGridDefaults();
        break;
    }

    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _applyEmaPullbackDefaults(): void {
    this.form.patchValue({ direction: "long" });

    this.trendFilterGroup.patchValue({
      enabled: true,
      type: "ema_cross",
      period: 200,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "long",
    });

    this.conditionsArray.clear();
    this.conditionsArray.push(this._conditionFactory.createPriceVsEmaCondition({
      label: "Price near EMA 50",
      period: 50,
      operator: "near",
      distanceType: "percent",
      distanceValue: 0.25,
    }));
    this.conditionsArray.push(this._conditionFactory.createRsiCondition({
      label: "RSI Oversold",
      period: 14,
      operator: "lt",
      value: 40,
    }));

    this.exitGroup.patchValue({
      takeProfit: { enabled: true, type: "fixed_percent", value: 3 },
      stopLoss: { enabled: true, type: "swing_low", value: null, lookback: 5 },
    });
  }

  private _applyMacdCrossDefaults(): void {
    this.form.patchValue({ direction: "long" });

    this.trendFilterGroup.patchValue({
      enabled: false,
      type: "ema_cross",
      period: 200,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "both",
    });

    this.conditionsArray.clear();
    this.conditionsArray.push(this._conditionFactory.createMacdCondition({
      label: "MACD Bullish Cross",
      fastPeriod: 12,
      slowPeriod: 26,
      signalPeriod: 9,
      operator: "cross_above_signal",
    }));

    this.exitGroup.patchValue({
      takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
      stopLoss: { enabled: true, type: "fixed_percent", value: 1.5 },
    });
  }

  private _resetGridDefaults(): void {
    this.gridGroup.patchValue({
      levels: 10,
      spacing: 0.5,
      entryMode: "auto_from_signal_candle",
      anchorPrice: null,
      breakdownThreshold: 1.5,
    });

    this.exitGroup.patchValue({
      takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
      stopLoss: { enabled: true, type: "fixed_percent", value: 6 },
    });
  }

  private _setupEntryStepValidation(): void {
    this.conditionsArray.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.entryStepForm.updateValueAndValidity());

    this.gridGroup.statusChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.entryStepForm.updateValueAndValidity());
  }

  private _entryStepValidator(): ValidatorFn {
    return (): ValidationErrors | null => {
      if (!this.form) {
        return null;
      }

      if (this.isSignalMode) {
        const conditions = this.form.get("conditions") as FormArray;

        return conditions && conditions.length > 0 ? null : { entryConditionsRequired: true };
      }

      const grid = this.form.get("grid") as FormGroup;

      return grid && grid.valid ? null : { gridInvalid: true };
    };
  }

  private _persistDraft(): void {
    const config = this.currentConfig;
    this._draftService.save(config);
  }

  private _populateFormFromDraft(config: StrategyConfig): void {
    this.form.patchValue({
      templateId: config.templateId ?? "grid",
      strategyName: config.strategyName,
      exchange: config.exchange,
      market: config.market,
      timeframe: config.timeframe,
      direction: config.direction,
    });

    if (config.grid) {
      this.form.get("grid")?.patchValue(config.grid);
    }

    if (config.exit) {
      this.form.get("exit")?.patchValue(config.exit);
    }

    if (config.risk) {
      this.form.get("risk")?.patchValue(config.risk);
    }

    if (config.trendFilter) {
      this.form.get("trendFilter")?.patchValue(config.trendFilter);
    }

    if (config.entryLogic) {
      this.form.patchValue({ entryLogic: config.entryLogic });
    }

    if (this._signalTemplates.has(config.templateId ?? "grid")) {
      this.form.get("grid")?.disable();
    }
  }
}
