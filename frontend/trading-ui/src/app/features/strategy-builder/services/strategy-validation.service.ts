import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, catchError, of } from "rxjs";
import { StrategyApiService } from "./strategy-api.service";
import {
  ServerValidationResult,
  StrategyConfig,
  ValidationError,
} from "../models/strategy.model";

@Injectable({ providedIn: "root" })
export class StrategyValidationService {
  private readonly _strategyApi = inject(StrategyApiService);

  public validate(formValue: Record<string, unknown>): ValidationError[] {
    const errors: ValidationError[] = [];
    const templateId = String(formValue["templateId"] ?? "grid");
    const strategyMode = String(formValue["strategyMode"] ?? "grid");
    const isSignalMode = strategyMode === "signal" || this._isSignalTemplate(templateId);
    const name = String(formValue["strategyName"] ?? "").trim();
    const market = String(formValue["market"] ?? "").trim();
    const timeframe = String(formValue["timeframe"] ?? "").trim();
    const grid = (formValue["grid"] ?? null) as Record<string, unknown> | null;
    const exit = (formValue["exit"] ?? null) as Record<string, unknown> | null;
    const risk = (formValue["risk"] ?? null) as Record<string, unknown> | null;
    const takeProfit = (exit?.["takeProfit"] ?? null) as Record<string, unknown> | null;
    const stopLoss = (exit?.["stopLoss"] ?? null) as Record<string, unknown> | null;

    if (name.length === 0) {
      errors.push(this._error("strategyName", "REQUIRED", "Strategy name is required."));
    } else if (name.length > 100) {
      errors.push(this._error("strategyName", "MAX_LENGTH", "Strategy name must be 100 characters or fewer."));
    }

    if (market.length === 0) {
      errors.push(this._error("market", "REQUIRED", "Market is required."));
    }

    if (timeframe.length === 0) {
      errors.push(this._error("timeframe", "REQUIRED", "Timeframe is required."));
    }

    if (isSignalMode) {
      this._validateSignalMode(formValue, errors);
    } else if (grid === null) {
      errors.push(this._error("grid", "REQUIRED", "Grid configuration is required."));
    } else {
      const levels = Number(grid["levels"] ?? 0);
      const spacing = Number(grid["spacing"] ?? 0);
      const breakdownThreshold = Number(grid["breakdownThreshold"] ?? 0);
      const entryMode = String(grid["entryMode"] ?? "auto_from_signal_candle");
      const anchorPrice = this._toNullableNumber(grid["anchorPrice"]);

      if (levels < 1 || levels > 50) {
        errors.push(this._error("grid.levels", "RANGE", "Grid levels must be between 1 and 50."));
      }

      if (spacing < 0.01 || spacing > 10) {
        errors.push(this._error("grid.spacing", "RANGE", "Grid spacing must be between 0.01% and 10%."));
      }

      if (breakdownThreshold < 0 || breakdownThreshold > 10) {
        errors.push(this._error("grid.breakdownThreshold", "RANGE", "Breakdown threshold must be between 0 and 10."));
      }

      if (entryMode === "manual" && (anchorPrice === null || anchorPrice <= 0)) {
        errors.push(this._error("grid.anchorPrice", "REQUIRED", "Anchor price is required for manual entry mode."));
      }
    }

    this._validateExitRule(takeProfit, "exit.takeProfit", "Take profit", errors);
    this._validateExitRule(stopLoss, "exit.stopLoss", "Stop loss", errors);

    if (risk !== null) {
      const positionSizeValue = Number(risk["positionSizeValue"] ?? 0);
      const leverage = Number(risk["leverage"] ?? 0);
      const maxOpenTrades = Number(risk["maxOpenTrades"] ?? 0);
      const cooldownValue = Number(risk["cooldownValue"] ?? 0);

      if (positionSizeValue < 0.01 || positionSizeValue > 100) {
        errors.push(this._error("risk.positionSizeValue", "RANGE", "Position size must be between 0.01 and 100."));
      }

      if (leverage < 1 || leverage > 50) {
        errors.push(this._error("risk.leverage", "RANGE", "Leverage must be between 1x and 50x."));
      }

      if (maxOpenTrades < 1 || maxOpenTrades > 10) {
        errors.push(this._error("risk.maxOpenTrades", "RANGE", "Max open trades must be between 1 and 10."));
      }

      if (cooldownValue < 0) {
        errors.push(this._error("risk.cooldownValue", "RANGE", "Cooldown cannot be negative."));
      }
    }

    return errors;
  }

  private _validateSignalMode(formValue: Record<string, unknown>, errors: ValidationError[]): void {
    const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];

    if (conditions.length === 0) {
      errors.push(this._error("entryConditions", "REQUIRED", "At least one entry condition required."));
      return;
    }

    const duplicateIndexes = this._findDuplicateConditionIndexes(conditions);

    duplicateIndexes.forEach((index) => {
      errors.push(this._error(`entryConditions[${index}]`, "DUPLICATE", "Duplicate entry conditions are not allowed."));
    });

    conditions.forEach((condition, index) => {
      const type = String(condition["type"] ?? "rsi");

      if (type === "macd") {
        this._validateMacdCondition(condition, index, errors);
        return;
      }

      if (type === "support_resistance") {
        this._validateSupportResistanceCondition(condition, index, errors);
        return;
      }

      const period = Number(condition["period"] ?? 0);

      if (period < 1) {
        const periodLabel = type === "price_vs_ema" ? "EMA period" : "RSI period";
        errors.push(this._error(`entryConditions[${index}].params.period`, "RANGE", `${periodLabel} must be at least 1.`));
      }

      if (type === "price_vs_ema") {
        this._validatePriceVsEmaCondition(condition, index, errors);
        return;
      }

      const value = Number(condition["value"] ?? -1);
      if (value < 0 || value > 100) {
        errors.push(this._error(`entryConditions[${index}].params.value`, "RANGE", "RSI value must be between 0 and 100."));
      }
    });
  }

  private _findDuplicateConditionIndexes(conditions: Record<string, unknown>[]): number[] {
    const seenKeys = new Map<string, number>();
    const duplicateIndexes = new Set<number>();

    conditions.forEach((condition, index) => {
      const signature = this._createConditionSignature(condition);
      const existingIndex = seenKeys.get(signature);

      if (existingIndex === undefined) {
        seenKeys.set(signature, index);
        return;
      }

      duplicateIndexes.add(existingIndex);
      duplicateIndexes.add(index);
    });

    return Array.from(duplicateIndexes).sort((left, right) => left - right);
  }

  private _createConditionSignature(condition: Record<string, unknown>): string {
    const type = String(condition["type"] ?? "rsi");

    if (type === "price_vs_ema") {
      return [
        type,
        String(condition["period"] ?? ""),
        String(condition["operator"] ?? ""),
        String(condition["distanceType"] ?? ""),
        String(condition["distanceValue"] ?? ""),
      ].join("|");
    }

    if (type === "macd") {
      return [
        type,
        String(condition["fastPeriod"] ?? ""),
        String(condition["slowPeriod"] ?? ""),
        String(condition["signalPeriod"] ?? ""),
        String(condition["operator"] ?? ""),
      ].join("|");
    }

    if (type === "support_resistance") {
      return [
        type,
        String(condition["lookback"] ?? ""),
        String(condition["strength"] ?? ""),
        String(condition["operator"] ?? ""),
        String(condition["tolerance"] ?? ""),
      ].join("|");
    }

    return [
      type,
      String(condition["period"] ?? ""),
      String(condition["operator"] ?? ""),
      String(condition["value"] ?? ""),
    ].join("|");
  }

  private _validateMacdCondition(condition: Record<string, unknown>, index: number, errors: ValidationError[]): void {
    const fastPeriod = Number(condition["fastPeriod"] ?? 0);
    const slowPeriod = Number(condition["slowPeriod"] ?? 0);
    const signalPeriod = Number(condition["signalPeriod"] ?? 0);

    if (fastPeriod < 2 || fastPeriod > 50) {
      errors.push(this._error(`entryConditions[${index}].params.fastPeriod`, "RANGE", "Fast period must be between 2 and 50."));
    }

    if (slowPeriod < 5 || slowPeriod > 200) {
      errors.push(this._error(`entryConditions[${index}].params.slowPeriod`, "RANGE", "Slow period must be between 5 and 200."));
    }

    if (signalPeriod < 2 || signalPeriod > 50) {
      errors.push(this._error(`entryConditions[${index}].params.signalPeriod`, "RANGE", "Signal period must be between 2 and 50."));
    }

    if (fastPeriod >= slowPeriod) {
      errors.push(this._error(`entryConditions[${index}].params.fastPeriod`, "RANGE", "Fast period must be less than slow period."));
    }
  }

  private _validatePriceVsEmaCondition(condition: Record<string, unknown>, index: number, errors: ValidationError[]): void {
    const operator = String(condition["operator"] ?? "near");

    if (operator !== "near") {
      return;
    }

    const distanceType = String(condition["distanceType"] ?? "").trim();
    const distanceValue = this._toNullableNumber(condition["distanceValue"]);

    if (distanceType.length === 0) {
      errors.push(this._error(`entryConditions[${index}].params.distanceType`, "REQUIRED", "Distance type is required for near EMA conditions."));
    }

    if (distanceValue === null) {
      errors.push(this._error(`entryConditions[${index}].params.distanceValue`, "REQUIRED", "Distance value is required for near EMA conditions."));
      return;
    }

    if (distanceValue <= 0) {
      errors.push(this._error(`entryConditions[${index}].params.distanceValue`, "RANGE", "Distance value must be greater than 0."));
    }
  }

  private _validateSupportResistanceCondition(condition: Record<string, unknown>, index: number, errors: ValidationError[]): void {
    const lookback = Number(condition["lookback"] ?? 0);
    const strength = Number(condition["strength"] ?? 0);
    const tolerance = Number(condition["tolerance"] ?? -1);

    if (lookback < 10 || lookback > 500) {
      errors.push(this._error(`entryConditions[${index}].params.lookback`, "RANGE", "Lookback must be between 10 and 500."));
    }

    if (strength < 1 || strength > 10) {
      errors.push(this._error(`entryConditions[${index}].params.strength`, "RANGE", "Strength must be between 1 and 10."));
    }

    if (tolerance < 0 || tolerance > 10) {
      errors.push(this._error(`entryConditions[${index}].params.tolerance`, "RANGE", "Tolerance must be between 0 and 10."));
    }
  }

  private _isSignalTemplate(templateId: string): boolean {
    return templateId === "custom_signal" || templateId === "ema_pullback" || templateId === "macd_cross";
  }

  public validateServer(config: StrategyConfig, context?: HttpContext): Observable<ServerValidationResult> {
    return this._strategyApi.validateStrategy(config, context).pipe(
      catchError(() => {
        return of({
          isValid: false,
          errors: [this._error("form", "VALIDATION_UNAVAILABLE", "Server validation is temporarily unavailable.")],
          warnings: [],
          infoMessages: [],
        });
      })
    );
  }

  private _validateExitRule(
    rule: Record<string, unknown> | null,
    fieldPath: string,
    label: string,
    errors: ValidationError[]
  ): void {
    if (rule === null || !rule["enabled"]) {
      return;
    }

    const type = String(rule["type"] ?? "fixed_percent");

    if (type === "swing_low") {
      const lookback = this._toNullableNumber(rule["lookback"]);

      if (lookback === null) {
        errors.push(this._error(`${fieldPath}.lookback`, "REQUIRED", `${label} lookback is required when swing low is enabled.`));
        return;
      }

      if (lookback < 1) {
        errors.push(this._error(`${fieldPath}.lookback`, "RANGE", `${label} lookback must be at least 1 candle.`));
      }

      return;
    }

    const value = this._toNullableNumber(rule["value"]);

    if (value === null) {
      errors.push(this._error(`${fieldPath}.value`, "REQUIRED", `${label} value is required when enabled.`));
      return;
    }

    if (value < 0.01 || value > 50) {
      errors.push(this._error(`${fieldPath}.value`, "RANGE", `${label} must be between 0.01% and 50%.`));
    }
  }

  private _toNullableNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === "") {
      return null;
    }

    const parsedValue = Number(value);
    return Number.isNaN(parsedValue) ? null : parsedValue;
  }

  private _error(fieldPath: string, code: string, message: string): ValidationError {
    return {
      severity: "error",
      fieldPath,
      code,
      message,
    };
  }
}