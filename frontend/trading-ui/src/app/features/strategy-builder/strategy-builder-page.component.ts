import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { DestroyRef, OnInit, Component, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog } from "@angular/material/dialog";
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
import { RiskManagementCardComponent } from "./components/risk-management-card/risk-management-card.component";
import { StrategyDetailsCardComponent } from "./components/strategy-details-card/strategy-details-card.component";
import { StrategyTemplateSelectorComponent } from "./components/strategy-template-selector/strategy-template-selector.component";
import { TrendFilterCardComponent } from "./components/trend-filter-card/trend-filter-card.component";
import { ValidationCardComponent } from "./components/validation-card/validation-card.component";
import { HasUnsavedChanges } from "./guards/unsaved-changes.guard";
import { ServerValidationResult, StrategyConfig, ValidationError } from "./models/strategy.model";
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
    MatProgressSpinnerModule,
    StrategyTemplateSelectorComponent,
    StrategyDetailsCardComponent,
    GridConfigCardComponent,
    ExitRulesCardComponent,
    RiskManagementCardComponent,
    TrendFilterCardComponent,
    EntryConditionsCardComponent,
    PreviewSummaryCardComponent,
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
  private readonly _notifications = inject(NotificationService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

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

    if (this.editId !== null) {
      this._loadStrategy(this.editId);
    }
  }

  public get pageTitle(): string {
    return this.editId === null ? "New Strategy" : "Edit Strategy";
  }

  public get pageSubtitle(): string {
    return this.editId === null
      ? "Build a grid strategy with the visual editor."
      : "Update the saved strategy configuration and review the resulting JSON.";
  }

  public get selectedTemplateId(): string {
    return String(this.form.get("templateId")?.value ?? "grid");
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

  public get canSave(): boolean {
    return this.form.valid && this.clientErrors.length === 0 && this.serverErrors.length === 0 && !this.isSaving && !this.isLoading;
  }

  public get allErrors(): ValidationError[] {
    return this.clientErrors.concat(this.serverErrors);
  }

  public onTemplateSelected(templateId: string): void {
    this.form.patchValue({ templateId });
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
    if (!this.form.dirty) {
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

      this.form.markAsPristine();
      void this._router.navigate(["/strategies"]);
    });
  }

  public hasUnsavedChanges(): boolean {
    return this.form.dirty;
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
        this.form.patchValue({
          templateId: strategy.config.templateId ?? "grid",
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
}