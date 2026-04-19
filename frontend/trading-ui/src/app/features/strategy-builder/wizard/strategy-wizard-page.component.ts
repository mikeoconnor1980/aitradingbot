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
import { DcaScalingBand, StrategyConfig, StrategyTemplateDto, ServerValidationResult, ValidationError } from "../models/strategy.model";
import { StrategyApiService } from "../services/strategy-api.service";
import { StrategyMapperService } from "../services/strategy-mapper.service";
import { StrategyValidationService } from "../services/strategy-validation.service";
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
  public readonly _education = inject(WizardEducationService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @ViewChild("stepper") public stepper!: MatStepper;

  private readonly _templateModeMap = new Map<string, "grid" | "signal" | "dca">([
    ["grid", "grid"],
    ["signal", "signal"],
    ["dca", "dca"],
  ]);
  private readonly _wizardStepEducationIndices: Record<"grid" | "signal" | "dca", number[]> = {
    grid: [0, 1, 2, 3, 4, 6],
    signal: [0, 1, 2, 3, 4, 5, 6],
    dca: [0, 1, 2, 6],
  };

  public form: FormGroup = this._buildForm();
  public marketStepForm: FormGroup = this._fb.group({}, { validators: [this._marketStepValidator()] });
  public entryStepForm: FormGroup = this._fb.group({}, { validators: [this._entryStepValidator()] });
  public isSaving = false;
  public isLoadingLibrary = false;
  public libraryTemplates: StrategyTemplateDto[] = [];
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

  public get selectedTemplateMode(): "grid" | "signal" | "dca" {
    return this._templateModeMap.get(this.selectedTemplateId) ?? "grid";
  }

  public get isSignalMode(): boolean {
    return this.selectedTemplateMode === "signal";
  }

  public get isDcaMode(): boolean {
    return this.selectedTemplateMode === "dca";
  }

  public get showFilterStep(): boolean {
    return this.isSignalMode;
  }

  public get showExitStep(): boolean {
    return !this.isDcaMode;
  }

  public get showRiskStep(): boolean {
    return !this.isDcaMode;
  }

  public get currentStepEducation(): WizardStepEducation {
    const visibleIndices = this._wizardStepEducationIndices[this.selectedTemplateMode] ?? this._wizardStepEducationIndices.grid;
    const educationIndex = visibleIndices[this.currentStepIndex] ?? visibleIndices[0] ?? 0;

    return this._education.getStepEducation(educationIndex);
  }

  public get marketGroup(): FormGroup {
    return this.form;
  }

  public get gridGroup(): FormGroup {
    return this.form.get("grid") as FormGroup;
  }

  public get dcaGroup(): FormGroup {
    return this.form.get("dca") as FormGroup;
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
    if (this.isDcaMode) {
      return "DCA schedule is incomplete";
    }

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

  public get availableTags(): string[] {
    const tags = new Set<string>();

    for (const template of this.libraryTemplates) {
      for (const tag of template.tags) {
        tags.add(tag);
      }
    }

    return Array.from(tags).sort();
  }

  public get tagsControl() {
    return this.form.get("metadata.tags");
  }

  public ngOnInit(): void {
    const draft = this._draftService.draft;

    this._loadLibraryTemplates();

    if (draft !== null) {
      this._populateFormFromDraft(draft);
    }

    this._setupValidationStream();
    this._setupMarketStepValidation();
    this._setupEntryStepValidation();
    this._setupAutoSaveDraft();
    this._setupAutoName();
  }

  public onTemplateSelected(templateId: string): void {
    this.form.patchValue({ templateId });
    this._applyModeState();
    this._applyTemplateDefaults(templateId);
    this._maybeAutoName();
    this.entryStepForm.updateValueAndValidity();
  }

  public onLibraryTemplateSelected(template: StrategyTemplateDto): void {
    this._applyLibraryTemplateToForm(template);
    this.stepper.next();
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
      dca: this._fb.group({
        interval: ["weekly", Validators.required],
        dayOfWeek: [1, [Validators.min(0), Validators.max(6)]],
        dayOfMonth: [null, [Validators.min(1), Validators.max(31)]],
        timeOfDayUtc: ["00:00", [Validators.required, Validators.pattern(/^([01]\d|2[0-3]):[0-5]\d$/)]],
        baseAmountUsd: [100, [Validators.required, Validators.min(0.01)]],
        gateConditions: this._fb.group({
          maxPriceUsd: [null, [Validators.min(0.00000001)]],
          minFearGreedIndex: [null, [Validators.min(0), Validators.max(100)]],
          maxFearGreedIndex: [null, [Validators.min(0), Validators.max(100)]],
        }),
        scalingBands: this._fb.array([]),
      }),
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

  private _setupMarketStepValidation(): void {
    this.form.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.marketStepForm.updateValueAndValidity({ emitEvent: false }));

    this.form.statusChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.marketStepForm.updateValueAndValidity({ emitEvent: false }));
  }

  private _marketStepValidator(): ValidatorFn {
    return (): ValidationErrors | null => {
      if (!this.form) {
        return null;
      }

      const exchangeControl = this.form.get("exchange");
      const marketControl = this.form.get("market");
      const strategyNameControl = this.form.get("strategyName");
      const timeframeControl = this.form.get("timeframe");
      const directionControl = this.form.get("direction");

      const hasVisibleControlError = [exchangeControl, marketControl, strategyNameControl].some((control) => control?.invalid === true)
        || (!this.isDcaMode && [timeframeControl, directionControl].some((control) => control?.invalid === true));

      return hasVisibleControlError ? { marketStepInvalid: true } : null;
    };
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
      signal: "Signal",
      dca: "DCA",
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
      case "signal":
        this.conditionsArray.clear();
        break;
      case "grid":
        this.conditionsArray.clear();
        this._resetGridDefaults();
        break;
      case "dca":
        this.conditionsArray.clear();
        this._resetDcaDefaults();
        break;
    }

    this.form.markAsDirty();
    this.form.updateValueAndValidity();
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

  private _resetDcaDefaults(): void {
    this.dcaGroup.patchValue({
      interval: "weekly",
      dayOfWeek: 1,
      dayOfMonth: null,
      timeOfDayUtc: "00:00",
      baseAmountUsd: 100,
      gateConditions: {
        maxPriceUsd: null,
        minFearGreedIndex: null,
        maxFearGreedIndex: null,
      },
    });
    this._setDcaScalingBands([]);
  }

  private _applyLibraryTemplateToForm(template: StrategyTemplateDto): void {
    const config = template.config;

    const templateId = config.strategyMode === "dca"
      ? "dca"
      : config.strategyMode === "signal"
        ? "signal"
        : "grid";
    this.form.patchValue({ templateId });
    this._applyModeState();

    this.form.patchValue({
      strategyName: template.name,
      exchange: config.exchange ?? "Hyperliquid",
      market: config.market,
      timeframe: config.timeframe,
      direction: config.direction,
      metadata: {
        tags: config.metadata?.tags ?? template.tags ?? [],
        notes: config.metadata?.notes ?? "",
      },
    });

    if (config.dca) {
      this.dcaGroup.patchValue({
        interval: config.dca.interval,
        dayOfWeek: config.dca.dayOfWeek ?? 1,
        dayOfMonth: config.dca.dayOfMonth ?? null,
        timeOfDayUtc: config.dca.timeOfDayUtc,
        baseAmountUsd: config.dca.baseAmountUsd,
        gateConditions: {
          maxPriceUsd: config.dca.gateConditions?.maxPriceUsd ?? null,
          minFearGreedIndex: config.dca.gateConditions?.minFearGreedIndex ?? null,
          maxFearGreedIndex: config.dca.gateConditions?.maxFearGreedIndex ?? null,
        },
      });
      this._setDcaScalingBands(config.dca.scalingBands ?? []);
    }

    this._userEditedName = true;

    if (config.grid) {
      this.gridGroup.patchValue(config.grid);
    }

    if (config.exit) {
      this.exitGroup.patchValue(config.exit);
    }

    if (config.risk) {
      this.riskGroup.patchValue(config.risk);
    }

    if (config.trendFilter) {
      this.trendFilterGroup.patchValue(config.trendFilter);
    }

    this.conditionsArray.clear();

    if (config.entryConditions) {
      for (const condition of config.entryConditions) {
        this.conditionsArray.push(this._fb.group(condition));
      }
    }

    if (config.entryLogic) {
      this.form.patchValue({ entryLogic: config.entryLogic });
    }

    this.entryStepForm.updateValueAndValidity();
    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _setupEntryStepValidation(): void {
    this.conditionsArray.valueChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.entryStepForm.updateValueAndValidity());

    this.gridGroup.statusChanges.pipe(
      takeUntilDestroyed(this._destroyRef)
    ).subscribe(() => this.entryStepForm.updateValueAndValidity());

    this.dcaGroup.statusChanges.pipe(
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

      if (this.isDcaMode) {
        const dca = this.form.get("dca") as FormGroup;

        return dca && dca.valid ? null : { dcaInvalid: true };
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
      metadata: config.metadata ?? { tags: [], notes: "" },
    });

    this._applyModeState();

    if (config.dca) {
      this.dcaGroup.patchValue({
        interval: config.dca.interval,
        dayOfWeek: config.dca.dayOfWeek ?? 1,
        dayOfMonth: config.dca.dayOfMonth ?? null,
        timeOfDayUtc: config.dca.timeOfDayUtc,
        baseAmountUsd: config.dca.baseAmountUsd,
        gateConditions: {
          maxPriceUsd: config.dca.gateConditions?.maxPriceUsd ?? null,
          minFearGreedIndex: config.dca.gateConditions?.minFearGreedIndex ?? null,
          maxFearGreedIndex: config.dca.gateConditions?.maxFearGreedIndex ?? null,
        },
      });
      this._setDcaScalingBands(config.dca.scalingBands ?? []);
    }

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

    this.form.updateValueAndValidity();
  }

  private _applyModeState(): void {
    if (this.isSignalMode || this.isDcaMode) {
      this.gridGroup.disable({ emitEvent: false });
    } else {
      this.gridGroup.enable({ emitEvent: false });
    }

    if (this.isDcaMode) {
      this.form.get("timeframe")?.setValue("1h", { emitEvent: false });
      this.form.get("direction")?.setValue("long", { emitEvent: false });
      this.form.get("timeframe")?.disable({ emitEvent: false });
      this.form.get("direction")?.disable({ emitEvent: false });
      return;
    }

    this.form.get("timeframe")?.enable({ emitEvent: false });
    this.form.get("direction")?.enable({ emitEvent: false });
    this.marketStepForm.updateValueAndValidity({ emitEvent: false });
  }

  private _setDcaScalingBands(bands: readonly DcaScalingBand[]): void {
    const scalingBands = this.dcaGroup.get("scalingBands") as FormArray;
    scalingBands.clear();

    for (const band of bands) {
      scalingBands.push(this._createDcaScalingBandGroup(band));
    }
  }

  private _createDcaScalingBandGroup(band?: DcaScalingBand): FormGroup {
    return this._fb.group({
      priceLowerUsd: [band?.priceLowerUsd ?? null, [Validators.min(0.00000001)]],
      priceUpperUsd: [band?.priceUpperUsd ?? null, [Validators.min(0.00000001)]],
      scalingPercent: [band?.scalingPercent ?? 0, [Validators.required, Validators.min(-100), Validators.max(500)]],
    });
  }

  private _loadLibraryTemplates(): void {
    this.isLoadingLibrary = true;

    this._strategyApi.getTemplates().subscribe({
      next: (templates) => {
        this.libraryTemplates = templates;
        this.isLoadingLibrary = false;
      },
      error: () => {
        this.isLoadingLibrary = false;
      }
    });
  }
}
