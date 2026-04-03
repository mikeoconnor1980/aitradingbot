import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { DestroyRef, OnInit, Component, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { ActivatedRoute, Router } from "@angular/router";
import { debounceTime, map, of, startWith, switchMap } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { NotificationService } from "../../core/services/notification.service";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { EntryConditionsCardComponent } from "./components/entry-conditions-card/entry-conditions-card.component";
import { ExitRulesCardComponent } from "./components/exit-rules-card/exit-rules-card.component";
import { GridConfigCardComponent } from "./components/grid-config-card/grid-config-card.component";
import { JsonPreviewCardComponent } from "./components/json-preview-card/json-preview-card.component";
import { PreviewSummaryCardComponent } from "./components/preview-summary-card/preview-summary-card.component";
import { RevisionHistoryPanelComponent } from "./components/revision-history-panel/revision-history-panel.component";
import { RiskManagementCardComponent } from "./components/risk-management-card/risk-management-card.component";
import { StrategyBacktestHistoryComponent } from "./components/strategy-backtest-history/strategy-backtest-history.component";
import { StrategyDetailsCardComponent } from "./components/strategy-details-card/strategy-details-card.component";
import { StrategyTemplateSelectorComponent } from "./components/strategy-template-selector/strategy-template-selector.component";
import { TrendFilterCardComponent } from "./components/trend-filter-card/trend-filter-card.component";
import { ValidationCardComponent } from "./components/validation-card/validation-card.component";
import { HasUnsavedChanges } from "./guards/unsaved-changes.guard";
import { EntryConditionConfig, ServerValidationResult, StrategyConfig, ValidationError } from "./models/strategy.model";
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
    StrategyTemplateSelectorComponent,
    StrategyDetailsCardComponent,
    GridConfigCardComponent,
    ExitRulesCardComponent,
    RiskManagementCardComponent,
    TrendFilterCardComponent,
    EntryConditionsCardComponent,
    PreviewSummaryCardComponent,
    RevisionHistoryPanelComponent,
    StrategyBacktestHistoryComponent,
    ValidationCardComponent,
    JsonPreviewCardComponent,
  ],
  templateUrl: "./strategy-builder-page.component.html",
  styleUrl: "./strategy-builder-page.component.scss"
})
export class StrategyBuilderPageComponent implements OnInit, HasUnsavedChanges {
  private readonly _fb = inject(FormBuilder);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _strategyMapper = inject(StrategyMapperService);
  private readonly _strategyValidator = inject(StrategyValidationService);
  private readonly _conditionFactory = inject(ConditionFactoryService);
  private readonly _notifications = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
  private _savedFormSnapshot = "";

  public form: FormGroup = this._buildForm();
  public editId: string | null = null;
  public isLoading = false;
  public isSaving = false;
  public isValidating = false;
  public clientErrors: ValidationError[] = [];
  public serverErrors: ValidationError[] = [];
  public serverWarnings: ValidationError[] = [];
  public serverInfoMessages: ValidationError[] = [];

  public ngOnInit(): void {
    this.editId = this._route.snapshot.paramMap.get("id");
    this._setupValidationStream();
    this._savedFormSnapshot = this._createFormSnapshot();

    if (this.editId !== null) {
      this._loadStrategy(this.editId);
    }
  }

  public get pageTitle(): string {
    return this.editId === null ? "New Strategy" : "Edit Strategy";
  }

  public get pageSubtitle(): string {
    if (this.editId !== null) {
      return "Update the saved strategy configuration and review the resulting JSON.";
    }

    return this.isSignalMode
      ? "Build a signal strategy with entry conditions."
      : "Build a grid strategy with the visual editor.";
  }

  public get selectedTemplateId(): string {
    return String(this.form.get("templateId")?.value ?? "grid");
  }

  public get isSignalMode(): boolean {
    return this.selectedTemplateId === "custom_signal";
  }

  public get currentConfig(): StrategyConfig {
    return this._strategyMapper.mapFormToConfig(this.form.getRawValue() as Record<string, unknown>);
  }

  public get gridFormGroup(): FormGroup {
    return this.form.get("grid") as FormGroup;
  }

  public get exitFormGroup(): FormGroup {
    return this.form.get("exit") as FormGroup;
  }

  public get riskFormGroup(): FormGroup {
    return this.form.get("risk") as FormGroup;
  }

  public get conditionsFormArray(): FormArray {
    return this.form.get("conditions") as FormArray;
  }

  public get canSave(): boolean {
    return this.hasUnsavedChanges() && this.form.valid && this.clientErrors.length === 0 && this.serverErrors.length === 0 && !this.isSaving && !this.isLoading;
  }

  public get allErrors(): ValidationError[] {
    return this.clientErrors.concat(this.serverErrors);
  }

  public onTemplateSelected(templateId: string): void {
    this.form.patchValue({ templateId });

    if (templateId === "custom_signal") {
      this.form.get("grid")?.disable();
      return;
    }

    this.form.get("grid")?.enable();
    this._clearConditions();
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

  public onBacktestStrategy(): void {
    if (this.editId === null) {
      return;
    }

    void this._router.navigate(["/backtesting"], {
      queryParams: { strategyId: this.editId }
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
          type: ["fixed_percent"],
          value: [2, [Validators.min(0.01), Validators.max(50)]],
        }),
        stopLoss: this._fb.group({
          enabled: [true],
          type: ["fixed_percent"],
          value: [6, [Validators.min(0.01), Validators.max(50)]],
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
      }),
      metadata: this._fb.group({
        tags: [[]],
        notes: [""],
      }),
      conditions: this._fb.array([]),
    });
  }

  private _setupValidationStream(): void {
    this.form.valueChanges.pipe(
      startWith(this.form.getRawValue()),
      debounceTime(250),
      map((value) => {
        const rawValue = value as Record<string, unknown>;
        this.clientErrors = this._strategyValidator.validate(rawValue);

        if (this.clientErrors.length > 0 || this.form.invalid) {
          this.serverErrors = [];
          this.serverWarnings = [];
          this.serverInfoMessages = [];
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
    this._strategyApi.getStrategy(id, this._localErrorContext).subscribe({
      next: (strategy) => {
        const templateId = strategy.config.templateId ?? (strategy.config.strategyMode === "signal" ? "custom_signal" : "grid");
        this.form.patchValue({
          templateId,
          strategyName: strategy.config.strategyName,
          exchange: strategy.config.exchange,
          market: strategy.config.market,
          timeframe: strategy.config.timeframe,
          direction: strategy.config.direction,
          grid: {
            levels: strategy.config.grid?.levels ?? 10,
            spacing: strategy.config.grid?.spacing ?? 0.5,
            entryMode: strategy.config.grid?.entryMode ?? "auto_from_signal_candle",
            anchorPrice: strategy.config.grid?.anchorPrice ?? null,
            breakdownThreshold: strategy.config.grid?.breakdownThreshold ?? 1.5,
          },
          exit: strategy.config.exit,
          risk: strategy.config.risk,
          metadata: strategy.config.metadata ?? { tags: [], notes: "" },
        });

        if (strategy.config.strategyMode === "signal") {
          this.form.patchValue({ templateId: strategy.config.templateId ?? "custom_signal" });
          this.form.get("grid")?.disable();
          this._clearConditions();

          for (const condition of strategy.config.entryConditions ?? []) {
            this._addLoadedCondition(condition);
          }
        } else {
          this.form.get("grid")?.enable();
          this._clearConditions();
        }

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

  private _clearConditions(): void {
    const conditions = this.conditionsFormArray;

    while (conditions.length > 0) {
      conditions.removeAt(0);
    }
  }

  private _addLoadedCondition(condition: EntryConditionConfig): void {
    if (condition.type !== "rsi") {
      return;
    }

    this.conditionsFormArray.push(this._conditionFactory.createRsiCondition({
      id: condition.id,
      enabled: condition.enabled,
      label: condition.label,
      period: condition.params.period,
      operator: condition.params.operator,
      value: condition.params.value,
    }));
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
}