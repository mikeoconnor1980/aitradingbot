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
    const isSignalMode = templateId === "custom_signal";
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

    this._validateExitRule(takeProfit, "exit.takeProfit.value", "Take profit", errors);
    this._validateExitRule(stopLoss, "exit.stopLoss.value", "Stop loss", errors);

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
      errors.push(this._error(`entryConditions[${index}]`, "DUPLICATE", "Duplicate RSI conditions are not allowed."));
    });

    conditions.forEach((condition, index) => {
      const period = Number(condition["period"] ?? 0);
      const value = Number(condition["value"] ?? -1);

      if (period < 1) {
        errors.push(this._error(`entryConditions[${index}].params.period`, "RANGE", "RSI period must be at least 1."));
      }

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
    return [
      String(condition["type"] ?? "rsi"),
      String(condition["period"] ?? ""),
      String(condition["operator"] ?? ""),
      String(condition["value"] ?? ""),
    ].join("|");
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

    const value = this._toNullableNumber(rule["value"]);

    if (value === null) {
      errors.push(this._error(fieldPath, "REQUIRED", `${label} value is required when enabled.`));
      return;
    }

    if (value < 0.01 || value > 50) {
      errors.push(this._error(fieldPath, "RANGE", `${label} must be between 0.01% and 50%.`));
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