import { Injectable } from "@angular/core";
import {
  Direction,
  EntryMode,
  EntryConditionConfig,
  EntryConditionType,
  ExitRuleType,
  MacdOperator,
  MacdParams,
  PriceVsEmaDistanceType,
  PriceVsEmaOperator,
  PositionSizeType,
  PriceVsEmaParams,
  TrendFilterConfig,
  TrendFilterType,
  TrendOperator,
  RsiOperator,
  RsiParams,
  StrategyConfig,
  SupportResistanceOperator,
  SupportResistanceParams,
} from "../models/strategy.model";

@Injectable({ providedIn: "root" })
export class StrategyMapperService {
  public mapFormToConfig(formValue: Record<string, unknown>): StrategyConfig {
    const grid = (formValue["grid"] ?? {}) as Record<string, unknown>;
    const exit = (formValue["exit"] ?? {}) as Record<string, unknown>;
    const takeProfit = (exit["takeProfit"] ?? {}) as Record<string, unknown>;
    const stopLoss = (exit["stopLoss"] ?? {}) as Record<string, unknown>;
    const risk = (formValue["risk"] ?? {}) as Record<string, unknown>;
    const metadata = (formValue["metadata"] ?? {}) as Record<string, unknown>;
    const source = (formValue["source"] ?? {}) as Record<string, unknown>;
    const trendFilter = (formValue["trendFilter"] ?? {}) as Record<string, unknown>;
    const entryMode = (grid["entryMode"] as EntryMode | undefined) ?? "auto_from_signal_candle";
    const templateId = String(formValue["templateId"] ?? "grid");
    const strategyMode = String(formValue["strategyMode"] ?? "grid");
    const isSignalMode = strategyMode === "signal" || this._isSignalTemplate(templateId);
    const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];
    const positionSizeType = (risk["positionSizeType"] as PositionSizeType | undefined) ?? "percent_wallet";

    return {
      schemaVersion: 1,
      strategyMode: isSignalMode ? "signal" : "grid",
      strategyName: String(formValue["strategyName"] ?? "").trim(),
      exchange: String(formValue["exchange"] ?? "Hyperliquid"),
      market: String(formValue["market"] ?? ""),
      timeframe: String(formValue["timeframe"] ?? "15m"),
      direction: (formValue["direction"] as Direction | undefined) ?? "long",
      enabled: true,
      templateId,
      grid: isSignalMode ? null : {
        levels: Number(grid["levels"] ?? 0),
        spacing: Number(grid["spacing"] ?? 0),
        entryMode,
        anchorPrice: entryMode === "manual" ? this._toNullableNumber(grid["anchorPrice"]) : null,
        breakdownThreshold: Number(grid["breakdownThreshold"] ?? 0),
      },
      trendFilter: isSignalMode ? this._mapTrendFilter(trendFilter) : null,
      entryLogic: isSignalMode ? (String(formValue["entryLogic"] ?? "all") as "all" | "any") : null,
      entryConditions: isSignalMode ? this._mapConditions(conditions) : null,
      exit: {
        takeProfit: {
          enabled: !!takeProfit["enabled"],
          type: (takeProfit["type"] as ExitRuleType | undefined) ?? "fixed_percent",
          value: takeProfit["enabled"] ? this._toNullableNumber(takeProfit["value"]) : null,
          lookback: null,
        },
        stopLoss: this._mapStopLoss(stopLoss),
        exitOnOppositeSignal: !!exit["exitOnOppositeSignal"],
      },
      risk: {
        positionSizeType,
        positionSizeValue: Number(risk["positionSizeValue"] ?? 0),
        leverage: Number(risk["leverage"] ?? 1),
        maxOpenTrades: Number(risk["maxOpenTrades"] ?? 1),
        cooldownValue: Number(risk["cooldownValue"] ?? 0),
        cooldownUnit: risk["cooldownUnit"] === "minutes" ? "minutes" : "candles",
        allowSameCandleReentry: !!risk["allowSameCandleReentry"],
        riskPerTradePercent: positionSizeType === "risk_based" ? Number(risk["riskPerTradePercent"] ?? 1) : undefined,
        autoLeverage: positionSizeType === "risk_based" ? Boolean(risk["autoLeverage"] ?? true) : undefined,
      },
      metadata: {
        tags: Array.isArray(metadata["tags"]) ? metadata["tags"].map((tag) => String(tag)) : [],
        notes: String(metadata["notes"] ?? ""),
      },
      source: {
        entryPoint: String(source["entryPoint"] ?? "ui_builder"),
        summary: String(source["summary"] ?? "Created in strategy builder"),
        sourceText: this._toNullableString(source["sourceText"]),
      },
    };
  }

  private _mapTrendFilter(trendFilter: Record<string, unknown>): TrendFilterConfig {
    const type = (trendFilter["type"] as TrendFilterType | undefined) ?? "ema_cross";

    return {
      enabled: Boolean(trendFilter["enabled"] ?? false),
      type,
      period: type === "price_above_ema" ? this._toNullableNumber(trendFilter["period"]) : null,
      fastPeriod: Number(trendFilter["fastPeriod"] ?? 50),
      slowPeriod: Number(trendFilter["slowPeriod"] ?? 200),
      operator: (trendFilter["operator"] as TrendOperator | undefined) ?? (type === "price_above_ema" ? "above" : "gt"),
      appliesTo: (trendFilter["appliesTo"] as Direction | undefined) ?? "both",
    };
  }

  private _mapStopLoss(stopLoss: Record<string, unknown>): { enabled: boolean; type: ExitRuleType; value?: number | null; lookback?: number | null; atrMultiplier?: number | null; trailingStopWarmup?: number | null } {
    const enabled = Boolean(stopLoss["enabled"] ?? false);
    const type = (stopLoss["type"] as ExitRuleType | undefined) ?? "fixed_percent";

    return {
      enabled,
      type,
      value: enabled && type === "fixed_percent" ? this._toNullableNumber(stopLoss["value"]) : null,
      lookback: enabled && type === "swing_low" ? this._toNullableNumber(stopLoss["lookback"]) : null,
      atrMultiplier: enabled && type === "atr_trailing" ? this._toNullableNumber(stopLoss["atr_multiplier"] ?? stopLoss["atrMultiplier"]) : null,
      trailingStopWarmup: enabled && type === "atr_trailing" ? this._toNullableNumber(stopLoss["trailing_stop_warmup"] ?? stopLoss["trailingStopWarmup"]) : null,
    };
  }

  private _mapConditions(conditions: Record<string, unknown>[]): EntryConditionConfig[] {
    return conditions.map((condition) => ({
      id: String(condition["id"] ?? ""),
      enabled: Boolean(condition["enabled"] ?? true),
      type: String(condition["type"] ?? "rsi") as EntryConditionType,
      label: String(condition["label"] ?? ""),
      params: this._mapConditionParams(condition),
    }));
  }

  private _mapConditionParams(condition: Record<string, unknown>): RsiParams | PriceVsEmaParams | MacdParams | SupportResistanceParams {
    const type = String(condition["type"] ?? "rsi");

    if (type === "price_vs_ema") {
      return {
        period: Number(condition["period"] ?? 50),
        operator: String(condition["operator"] ?? "near") as PriceVsEmaOperator,
        distanceType: String(condition["distanceType"] ?? "percent") as PriceVsEmaDistanceType,
        distanceValue: this._toNullableNumber(condition["distanceValue"]),
      };
    }

    if (type === "macd") {
      return {
        fastPeriod: Number(condition["fastPeriod"] ?? 12),
        slowPeriod: Number(condition["slowPeriod"] ?? 26),
        signalPeriod: Number(condition["signalPeriod"] ?? 9),
        operator: String(condition["operator"] ?? "cross_above_signal") as MacdOperator,
      };
    }

    if (type === "support_resistance") {
      return {
        lookback: Number(condition["lookback"] ?? 50),
        strength: Number(condition["strength"] ?? 3),
        operator: String(condition["operator"] ?? "near_support") as SupportResistanceOperator,
        tolerance: Number(condition["tolerance"] ?? 0.5),
      };
    }

    return {
      period: Number(condition["period"] ?? 14),
      operator: String(condition["operator"] ?? "lt") as RsiOperator,
      value: Number(condition["value"] ?? 40),
    };
  }

  private _isSignalTemplate(templateId: string): boolean {
    return templateId === "custom_signal" || templateId === "ema_pullback" || templateId === "macd_cross";
  }

  private _toNullableNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === "") {
      return null;
    }

    const parsedValue = Number(value);
    return Number.isNaN(parsedValue) ? null : parsedValue;
  }

  private _toNullableString(value: unknown): string | null {
    if (value === null || value === undefined) {
      return null;
    }

    const parsedValue = String(value).trim();
    return parsedValue.length === 0 ? null : parsedValue;
  }
}