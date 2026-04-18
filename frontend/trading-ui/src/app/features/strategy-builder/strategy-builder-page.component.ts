import { DOCUMENT } from "@angular/common";
import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { DestroyRef, OnInit, Component, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ActivatedRoute, Router } from "@angular/router";
import { debounceTime, map, of, startWith, switchMap, tap } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import { formatErrorPayload } from "../../core/utils/error-utils";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { AiReviewCardComponent } from "./components/ai-review-card/ai-review-card.component";
import { AiReviewModalComponent, AiReviewModalData } from "./components/ai-review-modal/ai-review-modal.component";
import {
  PromoteTemplateDialogComponent,
  PromoteTemplateDialogResult
} from "./components/promote-template-dialog/promote-template-dialog.component";
import { AssumptionsPanelComponent } from "./components/assumptions-panel/assumptions-panel.component";
import { ConfidenceBadgeComponent } from "./components/confidence-badge/confidence-badge.component";
import { DcaConfigCardComponent } from "./components/dca-config-card/dca-config-card.component";
import { EntryConditionsCardComponent } from "./components/entry-conditions-card/entry-conditions-card.component";
import { ExitRulesCardComponent } from "./components/exit-rules-card/exit-rules-card.component";
import { GridConfigCardComponent } from "./components/grid-config-card/grid-config-card.component";
import { JsonPreviewCardComponent } from "./components/json-preview-card/json-preview-card.component";
import { NlInputCardComponent } from "./components/nl-input-card/nl-input-card.component";
import { PreviewSummaryCardComponent } from "./components/preview-summary-card/preview-summary-card.component";
import { RevisionHistoryPanelComponent } from "./components/revision-history-panel/revision-history-panel.component";
import { RiskManagementCardComponent } from "./components/risk-management-card/risk-management-card.component";
import { StrategyBacktestHistoryComponent } from "./components/strategy-backtest-history/strategy-backtest-history.component";
import { StrategyDetailsCardComponent } from "./components/strategy-details-card/strategy-details-card.component";
import { StrategyTemplateSelectorComponent } from "./components/strategy-template-selector/strategy-template-selector.component";
import { TrendFilterCardComponent } from "./components/trend-filter-card/trend-filter-card.component";
import { ValidationCardComponent } from "./components/validation-card/validation-card.component";
import { HasUnsavedChanges } from "./guards/unsaved-changes.guard";
import { StrategyIntentDto } from "./models/strategy-intent.model";
import { StrategyReviewDto } from "./models/strategy-review.model";
import { CandlePatternParams, DcaScalingBand, EntryConditionConfig, LiquiditySweepParams, MacdParams, PriceVsEmaParams, RsiParams, ServerValidationResult, StrategyConfig, StrategyTemplateDto, StructureShiftParams, SupportResistanceParams, ValidationError } from "./models/strategy.model";
import { ConditionFactoryService } from "./services/condition-factory.service";
import { StrategyApiService } from "./services/strategy-api.service";
import { StrategyMapperService } from "./services/strategy-mapper.service";
import { StrategyValidationService } from "./services/strategy-validation.service";

@Component({
  selector: "app-strategy-builder-page",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    NlInputCardComponent,
    ConfidenceBadgeComponent,
    AssumptionsPanelComponent,
    StrategyTemplateSelectorComponent,
    StrategyDetailsCardComponent,
    DcaConfigCardComponent,
    GridConfigCardComponent,
    ExitRulesCardComponent,
    RiskManagementCardComponent,
    TrendFilterCardComponent,
    EntryConditionsCardComponent,
    PreviewSummaryCardComponent,
    AiReviewCardComponent,
    RevisionHistoryPanelComponent,
    StrategyBacktestHistoryComponent,
    ValidationCardComponent,
    JsonPreviewCardComponent,
  ],
  templateUrl: "./strategy-builder-page.component.html",
  styleUrl: "./strategy-builder-page.component.scss"
})
export class StrategyBuilderPageComponent implements OnInit, HasUnsavedChanges {
  private readonly _document = inject(DOCUMENT);
  private readonly _fb = inject(FormBuilder);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _strategyMapper = inject(StrategyMapperService);
  private readonly _strategyValidator = inject(StrategyValidationService);
  private readonly _conditionFactory = inject(ConditionFactoryService);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
  private _savedFormSnapshot = "";
  private _currentRevisionNumber: number | null = null;
  private _reviewCooldownKey: string | null = null;
  private _reviewCooldownEndsAtUtc = 0;
  private _cooldownIntervalId: ReturnType<typeof setInterval> | null = null;

  public form: FormGroup = this._buildForm();
  public editId: string | null = null;
  public nlResult: StrategyIntentDto | null = null;
  public nlSourceText = "";
  public isLoading = false;
  public isSaving = false;
  public isValidating = false;
  public clientErrors: ValidationError[] = [];
  public serverErrors: ValidationError[] = [];
  public serverWarnings: ValidationError[] = [];
  public serverInfoMessages: ValidationError[] = [];
  public isLoadingLibraryTemplates = false;
  public isPromotingTemplate = false;
  public libraryTemplates: StrategyTemplateDto[] = [];
  public currentReview: StrategyReviewDto | null = null;
  public isReviewing = false;
  public reviewCooldownSeconds = 0;

  public constructor() {
    this._destroyRef.onDestroy(() => {
      this._clearCooldownTimer();
    });
  }

  public ngOnInit(): void {
    this.editId = this._route.snapshot.paramMap.get("id");
    this._setupValidationStream();
    this._savedFormSnapshot = this._createFormSnapshot();

    if (this.editId !== null) {
      this._loadStrategy(this.editId);
      this._loadLibraryTemplates();
    } else {
      const duplicateFrom = this._route.snapshot.queryParamMap.get("duplicateFrom");
      if (duplicateFrom !== null) {
        this._duplicateStrategy(duplicateFrom);
      } else {
        const prefillConfig = history.state?.["prefillConfig"] as StrategyConfig | undefined;
        if (prefillConfig !== undefined) {
          this._populateFormFromIntent({ config: prefillConfig });
        }
      }
    }
  }

  public get pageTitle(): string {
    return this.editId === null ? "New Strategy" : "Edit Strategy";
  }

  public get pageSubtitle(): string {
    if (this.editId !== null) {
      return "Update the saved strategy configuration and review the resulting JSON.";
    }

    if (this.isDcaMode) {
      return "Build a scheduled spot DCA strategy with optional price and sentiment gates.";
    }

    return this.isSignalMode
      ? "Build a signal strategy with entry conditions."
      : "Build a grid strategy with the visual editor.";
  }

  public get selectedTemplateId(): string {
    return String(this.form.get("templateId")?.value ?? "grid");
  }

  public get isSignalMode(): boolean {
    return String(this.form.get("strategyMode")?.value ?? "grid") === "signal";
  }

  public get isDcaMode(): boolean {
    return String(this.form.get("strategyMode")?.value ?? "grid") === "dca";
  }

  public get currentConfig(): StrategyConfig {
    return this._strategyMapper.mapFormToConfig(this.form.getRawValue() as Record<string, unknown>);
  }

  public get gridFormGroup(): FormGroup {
    return this.form.get("grid") as FormGroup;
  }

  public get dcaFormGroup(): FormGroup {
    return this.form.get("dca") as FormGroup;
  }

  public get exitFormGroup(): FormGroup {
    return this.form.get("exit") as FormGroup;
  }

  public get riskFormGroup(): FormGroup {
    return this.form.get("risk") as FormGroup;
  }

  public get trendFilterFormGroup(): FormGroup {
    return this.form.get("trendFilter") as FormGroup;
  }

  public get conditionsFormArray(): FormArray {
    return this.form.get("conditions") as FormArray;
  }

  public get dcaScalingBandsFormArray(): FormArray {
    return this.form.get("dca.scalingBands") as FormArray;
  }

  public get canSave(): boolean {
    return this.hasUnsavedChanges() && this.form.valid && this.clientErrors.length === 0 && this.serverErrors.length === 0 && !this.isSaving && !this.isLoading;
  }

  public get allErrors(): ValidationError[] {
    return this.clientErrors.concat(this.serverErrors);
  }

  public get canRequestReview(): boolean {
    return this.editId !== null && this._currentRevisionNumber !== null && !this.isReviewing && !this.isReviewCooldownActive;
  }

  public get canPromoteToLibrary(): boolean {
    return this.editId !== null &&
      !this.hasUnsavedChanges() &&
      !this.isLoading &&
      !this.isSaving &&
      !this.isPromotingTemplate &&
      !this.isLoadingLibraryTemplates;
  }

  public get promoteButtonLabel(): string {
    if (this.isLoadingLibraryTemplates) {
      return "Loading Library";
    }

    if (this.isPromotingTemplate) {
      return "Promoting...";
    }

    return "Promote to Library";
  }

  public get promoteTooltip(): string {
    if (this.editId === null) {
      return "Save the strategy before promoting it to the library.";
    }

    if (this.hasUnsavedChanges()) {
      return "Save your latest changes before promoting this strategy to the shared library.";
    }

    if (this.isLoadingLibraryTemplates) {
      return "Loading the current library tag list.";
    }

    if (this.isPromotingTemplate) {
      return "Promotion is in progress.";
    }

    return "";
  }

  public get availableLibraryTags(): string[] {
    const tags = new Set<string>();

    for (const template of this.libraryTemplates) {
      for (const tag of template.tags) {
        tags.add(tag);
      }
    }

    return Array.from(tags).sort();
  }

  public get aiReviewButtonLabel(): string {
    if (this.isReviewCooldownActive) {
      return `AI Review (${this.reviewCooldownSeconds}s)`;
    }

    return "AI Review";
  }

  public get aiReviewTooltip(): string {
    if (this.editId === null) {
      return "Save the strategy before requesting an AI review.";
    }

    if (this.isReviewCooldownActive) {
      return `Try again in ${this.reviewCooldownSeconds} seconds.`;
    }

    if (this.isReviewing) {
      return "AI review in progress.";
    }

    return "";
  }

  public get isReviewCooldownActive(): boolean {
    return this.reviewCooldownSeconds > 0 && this._reviewCooldownKey === this._getCurrentReviewKey();
  }

  public onTemplateSelected(templateId: string): void {
    const mode = this._isDcaTemplate(templateId) ? "dca"
      : this._isSignalTemplate(templateId) ? "signal"
      : "grid";

    this.form.patchValue({ templateId, strategyMode: mode });

    this._applyModeState(templateId);

    if (mode === "dca") {
      this._applyDcaTemplate();
      return;
    }

    if (mode === "signal") {
      if (templateId === "ema_pullback") {
        this._applyEmaPullbackTemplate();
      } else if (templateId === "macd_cross") {
        this._applyMacdCrossTemplate();
      }

      return;
    }
  }

  public onSave(): void {
    this.form.markAllAsTouched();

    if (!this.canSave) {
      return;
    }

    this.isSaving = true;
    const config = this.currentConfig;
    const observer = {
      next: () => {
        this._savedFormSnapshot = this._createFormSnapshot();
        this.form.markAsPristine();
        this._notifications.success(`Strategy '${config.strategyName}' saved`);
        void this._router.navigate(["/strategies"]);
      },
      error: (error: HttpErrorResponse) => {
        this._applyServerSaveError(error);
        this.isSaving = false;
      },
      complete: () => {
        this.isSaving = false;
      }
    };

    if (this.editId === null) {
      this._strategyApi.createStrategy(config, this._localErrorContext).subscribe(observer);
      return;
    }

    this._strategyApi.updateStrategy(this.editId, config, this._localErrorContext).subscribe(observer);
  }

  public onCancel(): void {
    if (!this.hasUnsavedChanges()) {
      void this._router.navigate(["/strategies"]);
      return;
    }

    const dialogData: ConfirmDialogData = {
      title: "Unsaved Changes",
      message: "You have unsaved changes that will be lost.",
      confirmText: "Leave",
      cancelText: "Stay"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "400px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this._savedFormSnapshot = this._createFormSnapshot();
      this.form.markAsPristine();
      void this._router.navigate(["/strategies"]);
    });
  }

  public hasUnsavedChanges(): boolean {
    return this._createFormSnapshot() !== this._savedFormSnapshot;
  }

  public onNlInterpreted(result: StrategyIntentDto): void {
    if (!this._shouldConfirmInterpretation()) {
      this._applyInterpretedResult(result);
      return;
    }

    const dialogData: ConfirmDialogData = {
      title: "Replace Current Form?",
      message: "This will replace the current form values with the generated strategy configuration.",
      confirmText: "Replace",
      cancelText: "Keep Current"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "420px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this._applyInterpretedResult(result);
    });
  }

  public onEditAssumptionField(fieldName: string): void {
    const fieldSelector = this._buildFieldSelector(fieldName);
    const element = this._document.querySelector(fieldSelector) as HTMLElement | null;
    element?.scrollIntoView({ behavior: "smooth", block: "center" });
    element?.focus();
  }

  public onBacktestStrategy(): void {
    if (this.editId === null) {
      return;
    }

    void this._router.navigate(["/backtesting"], {
      queryParams: { strategyId: this.editId }
    });
  }

  public onRequestReview(): void {
    if (!this.canRequestReview || this.editId === null || this._currentRevisionNumber === null) {
      return;
    }

    this.isReviewing = true;

    this._strategyApi.requestReview(this.editId, this._currentRevisionNumber, this._localErrorContext).subscribe({
      next: (review: StrategyReviewDto) => {
        this.currentReview = review;
        this._startReviewCooldown();
        this._notifications.success("AI review complete.");
      },
      error: (error: HttpErrorResponse) => {
        this._notifications.error(this._buildReviewErrorMessage(error));
        this.isReviewing = false;
      },
      complete: () => {
        this.isReviewing = false;
      }
    });
  }

  public onPromoteToLibrary(): void {
    if (!this.canPromoteToLibrary || this.editId === null) {
      return;
    }

    const dialogRef = this._dialog.open(PromoteTemplateDialogComponent, {
      width: "640px",
      maxWidth: "92vw",
      autoFocus: false,
      data: {
        defaultName: this.currentConfig.strategyName,
        existingNames: this.libraryTemplates.map((template) => template.name),
        availableTags: this.availableLibraryTags,
        initialTags: this.currentConfig.metadata?.tags ?? [],
      }
    });

    dialogRef.afterClosed().subscribe((result: PromoteTemplateDialogResult | undefined) => {
      if (result === undefined || this.editId === null) {
        return;
      }

      this.isPromotingTemplate = true;
      this._strategyApi.promoteStrategyTemplate(this.editId, result, this._localErrorContext).subscribe({
        next: () => {
          this._notifications.success("Strategy promoted to the library.");
          this._loadLibraryTemplates();
        },
        error: (error: HttpErrorResponse) => {
          this._notifications.error(formatErrorPayload(error));
          this.isPromotingTemplate = false;
        },
        complete: () => {
          this.isPromotingTemplate = false;
        }
      });
    });
  }

  public onViewFullReview(): void {
    if (this.currentReview === null) {
      return;
    }

    const dialogData: AiReviewModalData = {
      review: this.currentReview
    };

    this._dialog.open(AiReviewModalComponent, {
      data: dialogData,
      width: "960px",
      maxWidth: "90vw",
      maxHeight: "90vh",
      autoFocus: false
    });
  }

  public onRevisionRestored(): void {
    if (this.editId === null) {
      return;
    }

    this._loadStrategy(this.editId);
  }

  private _buildForm(): FormGroup {
    return this._fb.group({
      templateId: ["grid"],
      strategyMode: ["grid"],
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
        entryPoint: ["ui_builder"],
        summary: ["Created in strategy builder"],
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
      startWith(null),
      debounceTime(250),
      map(() => this.form.getRawValue() as Record<string, unknown>),
      tap((rawValue) => {
        this.clientErrors = this._strategyValidator.validate(rawValue);

        if (this.clientErrors.length > 0 || this.form.invalid) {
          this.serverErrors = [];
          this.serverWarnings = [];
          this.serverInfoMessages = [];
        }
      }),
      map((rawValue) => {
        if (this.clientErrors.length > 0 || this.form.invalid) {
          return null;
        }

        return this._strategyMapper.mapFormToConfig(rawValue);
      }),
      switchMap((config) => {
        if (config === null) {
          this.isValidating = false;
          return of<ServerValidationResult | null>(null);
        }

        this.isValidating = true;
        return this._strategyValidator.validateServer(config, this._localErrorContext);
      }),
      takeUntilDestroyed(this._destroyRef)
    ).subscribe((result) => {
      this.isValidating = false;

      if (result === null) {
        return;
      }

      this.serverErrors = result.errors;
      this.serverWarnings = result.warnings;
      this.serverInfoMessages = result.infoMessages;
    });
  }

  private _loadStrategy(id: string): void {
    this.isLoading = true;
    this.currentReview = null;
    this._currentRevisionNumber = null;

    this._strategyApi.getStrategy(id, this._localErrorContext).subscribe({
      next: (strategy) => {
        this._applyConfigToForm(strategy.config);

        this.nlSourceText = strategy.config.source?.sourceText ?? "";
        this.nlResult = null;
        this._currentRevisionNumber = strategy.version;
        this._loadReviewIfAvailable(id, strategy.version);

        this._savedFormSnapshot = this._createFormSnapshot();
        this.form.markAsPristine();
        this.isLoading = false;
      },
      error: () => {
        this._notifications.error("Failed to load strategy.");
        this.isLoading = false;
        void this._router.navigate(["/strategies"]);
      }
    });
  }

  private _duplicateStrategy(sourceId: string): void {
    this.isLoading = true;

    this._strategyApi.getStrategy(sourceId, this._localErrorContext).subscribe({
      next: (strategy) => {
        this._applyConfigToForm({
          ...strategy.config,
          strategyName: `${strategy.config.strategyName} (Copy)`,
          metadata: { tags: [], notes: "" },
        });

        this.nlSourceText = strategy.config.source?.sourceText ?? "";
        this.nlResult = null;

        this._savedFormSnapshot = this._createFormSnapshot();
        this.form.markAsDirty();
        this.isLoading = false;
      },
      error: () => {
        this._notifications.error("Failed to load strategy for duplication.");
        this.isLoading = false;
      }
    });
  }

  private _clearConditions(): void {
    const conditions = this.conditionsFormArray;

    while (conditions.length > 0) {
      conditions.removeAt(0);
    }
  }

  private _applyEmaPullbackTemplate(): void {
    this.form.patchValue({
      direction: "long",
    });

    const trendFilterGroup = this.form.get("trendFilter") as FormGroup | null;
    if (trendFilterGroup !== null) {
      trendFilterGroup.patchValue({
        enabled: true,
        type: "ema_cross",
        period: 200,
        fastPeriod: 50,
        slowPeriod: 200,
        operator: "gt",
        appliesTo: "long",
      });
    }

    const conditionsArray = this.conditionsFormArray;
    conditionsArray.clear();
    conditionsArray.push(this._conditionFactory.createPriceVsEmaCondition({
      label: "Price near EMA 50",
      period: 50,
      operator: "near",
      distanceType: "percent",
      distanceValue: 0.25,
    }));
    conditionsArray.push(this._conditionFactory.createRsiCondition({
      label: "RSI Oversold",
      period: 14,
      operator: "lt",
      value: 40,
    }));

    const exitGroup = this.form.get("exit") as FormGroup;
    exitGroup.patchValue({
      takeProfit: {
        enabled: true,
        type: "fixed_percent",
        value: 3,
      },
      stopLoss: {
        enabled: true,
        type: "swing_low",
        value: null,
        lookback: 5,
      },
    });

    trendFilterGroup?.markAsDirty();
    conditionsArray.markAsDirty();
    exitGroup.markAsDirty();
    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _applyMacdCrossTemplate(): void {
    this.form.patchValue({
      direction: "long",
    });

    const trendFilterGroup = this.form.get("trendFilter") as FormGroup | null;
    trendFilterGroup?.patchValue({
      enabled: false,
      type: "ema_cross",
      period: 200,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "both",
    });

    const conditionsArray = this.conditionsFormArray;
    conditionsArray.clear();
    conditionsArray.push(this._conditionFactory.createMacdCondition({
      label: "MACD Bullish Cross",
      fastPeriod: 12,
      slowPeriod: 26,
      signalPeriod: 9,
      operator: "cross_above_signal",
    }));

    const exitGroup = this.form.get("exit") as FormGroup;
    exitGroup.patchValue({
      takeProfit: {
        enabled: true,
        type: "fixed_percent",
        value: 2,
      },
      stopLoss: {
        enabled: true,
        type: "fixed_percent",
        value: 1.5,
      },
    });

    trendFilterGroup?.markAsDirty();
    conditionsArray.markAsDirty();
    exitGroup.markAsDirty();
    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _applyDcaTemplate(): void {
    this.form.get("direction")?.setValue("long", { emitEvent: false });
    this.form.get("timeframe")?.setValue("1h", { emitEvent: false });
    this.dcaFormGroup.patchValue({
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
    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _addLoadedCondition(condition: EntryConditionConfig): void {
    if (condition.type === "candle_pattern") {
      const params = condition.params as CandlePatternParams;

      this.conditionsFormArray.push(this._conditionFactory.createCandlePatternCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        pattern: params.pattern,
      }));
      return;
    }

    if (condition.type === "liquidity_sweep") {
      const params = condition.params as LiquiditySweepParams;

      this.conditionsFormArray.push(this._conditionFactory.createLiquiditySweepCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        lookbackBars: params.lookbackBars,
        pivotBars: params.pivotBars,
        side: params.side,
      }));
      return;
    }

    if (condition.type === "structure_shift") {
      const params = condition.params as StructureShiftParams;

      this.conditionsFormArray.push(this._conditionFactory.createStructureShiftCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        pivotBars: params.pivotBars,
        direction: params.direction,
      }));
      return;
    }

    if (condition.type === "price_vs_ema") {
      const params = condition.params as PriceVsEmaParams;

      this.conditionsFormArray.push(this._conditionFactory.createPriceVsEmaCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        period: params.period,
        operator: params.operator,
        distanceType: params.distanceType,
        distanceValue: params.distanceValue,
      }));
      return;
    }

    if (condition.type === "macd") {
      const params = condition.params as MacdParams;

      this.conditionsFormArray.push(this._conditionFactory.createMacdCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        fastPeriod: params.fastPeriod,
        slowPeriod: params.slowPeriod,
        signalPeriod: params.signalPeriod,
        operator: params.operator,
      }));
      return;
    }

    if (condition.type === "support_resistance") {
      const params = condition.params as SupportResistanceParams;

      this.conditionsFormArray.push(this._conditionFactory.createSupportResistanceCondition({
        id: condition.id,
        enabled: condition.enabled,
        label: condition.label,
        lookback: params.lookback,
        strength: params.strength,
        operator: params.operator,
        tolerance: params.tolerance,
      }));
      return;
    }

    if (condition.type !== "rsi") {
      return;
    }

    const params = condition.params as RsiParams;

    this.conditionsFormArray.push(this._conditionFactory.createRsiCondition({
      id: condition.id,
      enabled: condition.enabled,
      label: condition.label,
      period: params.period,
      operator: params.operator,
      value: params.value,
    }));
  }

  private _applyConfigToForm(config: StrategyConfig): void {
    const templateId = this._resolveTemplateId(config);

    this.form.patchValue({
      templateId,
      strategyMode: config.strategyMode ?? "grid",
      strategyName: config.strategyName,
      exchange: config.exchange,
      market: config.market,
      timeframe: config.timeframe,
      direction: config.direction,
      grid: {
        levels: config.grid?.levels ?? 10,
        spacing: config.grid?.spacing ?? 0.5,
        entryMode: config.grid?.entryMode ?? "auto_from_signal_candle",
        anchorPrice: config.grid?.anchorPrice ?? null,
        breakdownThreshold: config.grid?.breakdownThreshold ?? 1.5,
      },
      dca: {
        interval: config.dca?.interval ?? "weekly",
        dayOfWeek: config.dca?.dayOfWeek ?? 1,
        dayOfMonth: config.dca?.dayOfMonth ?? null,
        timeOfDayUtc: config.dca?.timeOfDayUtc ?? "00:00",
        baseAmountUsd: config.dca?.baseAmountUsd ?? 100,
        gateConditions: {
          maxPriceUsd: config.dca?.gateConditions?.maxPriceUsd ?? null,
          minFearGreedIndex: config.dca?.gateConditions?.minFearGreedIndex ?? null,
          maxFearGreedIndex: config.dca?.gateConditions?.maxFearGreedIndex ?? null,
        },
      },
      exit: config.exit,
      risk: config.risk,
      metadata: config.metadata ?? { tags: [], notes: "" },
      source: {
        entryPoint: config.source?.entryPoint ?? "ui_builder",
        summary: config.source?.summary ?? "Created in strategy builder",
        sourceText: config.source?.sourceText ?? null,
      },
      trendFilter: config.trendFilter ?? {
        enabled: false,
        type: "ema_cross",
        period: 200,
        fastPeriod: 50,
        slowPeriod: 200,
        operator: "gt",
        appliesTo: "both",
      },
      entryLogic: config.entryLogic ?? "all",
    }, { emitEvent: false });

    this._setDcaScalingBands(config.dca?.scalingBands ?? []);
    this._clearConditions();
    this._applyModeState(templateId);

    if (config.strategyMode === "signal") {
      for (const condition of config.entryConditions ?? []) {
        this._addLoadedCondition(condition);
      }
    }

    this.form.updateValueAndValidity({ emitEvent: false });
  }

  private _applyModeState(_templateId: string): void {
    const isSignalMode = this.isSignalMode;
    const isDcaMode = this.isDcaMode;

    if (isSignalMode || isDcaMode) {
      this.form.get("grid")?.disable({ emitEvent: false });
    } else {
      this.form.get("grid")?.enable({ emitEvent: false });
    }

    if (isDcaMode) {
      this.form.get("direction")?.setValue("long", { emitEvent: false });
      this.form.get("timeframe")?.setValue("1h", { emitEvent: false });
      this.form.get("direction")?.disable({ emitEvent: false });
      this.form.get("timeframe")?.disable({ emitEvent: false });
      this._clearConditions();
      return;
    }

    this.form.get("direction")?.enable({ emitEvent: false });
    this.form.get("timeframe")?.enable({ emitEvent: false });

    if (!isSignalMode) {
      this._clearConditions();
    }
  }

  private _setDcaScalingBands(bands: readonly DcaScalingBand[]): void {
    this.dcaScalingBandsFormArray.clear();

    for (const band of bands) {
      this.dcaScalingBandsFormArray.push(this._createScalingBandGroup(band));
    }
  }

  private _createScalingBandGroup(band?: DcaScalingBand): FormGroup {
    return this._fb.group({
      priceLowerUsd: [band?.priceLowerUsd ?? null, [Validators.min(0.00000001)]],
      priceUpperUsd: [band?.priceUpperUsd ?? null, [Validators.min(0.00000001)]],
      scalingPercent: [band?.scalingPercent ?? 0, [Validators.required, Validators.min(-100), Validators.max(500)]],
    });
  }

  private _isSignalTemplate(templateId: string): boolean {
    return templateId === "custom_signal" || templateId === "ema_pullback" || templateId === "macd_cross";
  }

  private _isDcaTemplate(templateId: string): boolean {
    return templateId === "dca";
  }

  private _resolveTemplateId(config: StrategyConfig): string {
    if (config.templateId !== null && config.templateId !== undefined) {
      return config.templateId;
    }

    if (config.strategyMode === "signal") {
      return "custom_signal";
    }

    if (config.strategyMode === "dca") {
      return "dca";
    }

    return "grid";
  }

  private _applyServerSaveError(error: HttpErrorResponse): void {
    const errorMessage = typeof error.error === "object" && error.error !== null && "errorMessage" in error.error
      ? String(error.error["errorMessage"])
      : "Server validation failed.";
    const errorCode = typeof error.error === "object" && error.error !== null && "errorCode" in error.error
      ? String(error.error["errorCode"])
      : "server_error";
    const fieldPath = errorCode === "duplicate_name" ? "strategyName" : "form";

    this.serverErrors = [{
      severity: "error",
      fieldPath,
      code: errorCode,
      message: errorMessage,
    }];
    this.serverWarnings = [];
    this.serverInfoMessages = [];
  }

  private _createFormSnapshot(): string {
    return JSON.stringify(this.form.getRawValue());
  }

  private _loadLibraryTemplates(): void {
    this.isLoadingLibraryTemplates = true;

    this._strategyApi.getTemplates(this._localErrorContext).subscribe({
      next: (templates) => {
        this.libraryTemplates = templates;
        this.isLoadingLibraryTemplates = false;
      },
      error: () => {
        this.isLoadingLibraryTemplates = false;
      }
    });
  }

  private _applyInterpretedResult(result: StrategyIntentDto): void {
    this.nlResult = result;
    this.nlSourceText = result.config.source?.sourceText ?? "";
    this._populateFormFromIntent(result);
  }

  private _populateFormFromIntent(intent: { config: StrategyConfig }): void {
    const config = intent.config;
    const existingName = String(this.form.get("strategyName")?.value ?? "").trim();

    this._applyConfigToForm({
      ...config,
      strategyName: existingName.length > 0 ? existingName : config.strategyName,
    });

    this.form.markAsDirty();
    this.form.updateValueAndValidity();
  }

  private _shouldConfirmInterpretation(): boolean {
    if (this.editId !== null) {
      return true;
    }

    return this.form.dirty;
  }

  private _buildFieldSelector(fieldName: string): string {
    const normalizedFieldName = fieldName.trim();
    if (!normalizedFieldName.includes(".")) {
      return `[formcontrolname="${normalizedFieldName}"]`;
    }

    const segments = normalizedFieldName.split(".");
    const controlName = segments.pop() as string;
    const groupSelector = segments.map((segment: string) => `[formgroupname="${segment}"]`).join(" ");
    return `${groupSelector} [formcontrolname="${controlName}"]`;
  }

  private _loadReviewIfAvailable(strategyId: string, revisionNumber: number): void {
    this.currentReview = null;
    this._syncCooldownState();

    this._strategyApi.getReview(strategyId, revisionNumber, this._localErrorContext).subscribe({
      next: (review: StrategyReviewDto) => {
        this.currentReview = review;
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          return;
        }

        this._notifications.error("Failed to load AI review.");
      }
    });
  }

  private _buildReviewErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 429) {
      return "AI review is limited to one request per minute. Please wait and try again.";
    }

    if (error.status === 0) {
      return "Unable to reach API. Check your connection and try again.";
    }

    return formatErrorPayload(error);
  }

  private _startReviewCooldown(): void {
    const currentKey = this._getCurrentReviewKey();
    if (currentKey === null) {
      return;
    }

    this._reviewCooldownKey = currentKey;
    this._reviewCooldownEndsAtUtc = Date.now() + 60_000;
    this._clearCooldownTimer();
    this._updateReviewCooldownSeconds();

    this._cooldownIntervalId = setInterval(() => {
      this._updateReviewCooldownSeconds();

      if (this.reviewCooldownSeconds === 0) {
        this._clearCooldownTimer();
      }
    }, 1000);
  }

  private _syncCooldownState(): void {
    if (this._reviewCooldownKey !== this._getCurrentReviewKey()) {
      this.reviewCooldownSeconds = 0;
      this._clearCooldownTimer();
      return;
    }

    this._updateReviewCooldownSeconds();

    if (this.reviewCooldownSeconds > 0 && this._cooldownIntervalId === null) {
      this._cooldownIntervalId = setInterval(() => {
        this._updateReviewCooldownSeconds();

        if (this.reviewCooldownSeconds === 0) {
          this._clearCooldownTimer();
        }
      }, 1000);
    }
  }

  private _updateReviewCooldownSeconds(): void {
    const millisecondsRemaining = this._reviewCooldownEndsAtUtc - Date.now();
    this.reviewCooldownSeconds = Math.max(0, Math.ceil(millisecondsRemaining / 1000));

    if (this.reviewCooldownSeconds === 0) {
      this._reviewCooldownKey = null;
      this._reviewCooldownEndsAtUtc = 0;
    }
  }

  private _clearCooldownTimer(): void {
    if (this._cooldownIntervalId !== null) {
      clearInterval(this._cooldownIntervalId);
      this._cooldownIntervalId = null;
    }
  }

  private _getCurrentReviewKey(): string | null {
    if (this.editId === null || this._currentRevisionNumber === null) {
      return null;
    }

    return `${this.editId}:${this._currentRevisionNumber}`;
  }
}