import { Injectable } from "@angular/core";
import {
  Direction,
  EntryMode,
  EntryConditionConfig,
  EntryConditionType,
  PositionSizeType,
  RsiOperator,
  StrategyConfig,
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
    const entryMode = (grid["entryMode"] as EntryMode | undefined) ?? "auto_from_signal_candle";
    const templateId = String(formValue["templateId"] ?? "grid");
    const isSignalMode = templateId === "custom_signal";
    const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];

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
      trendFilter: null,
      entryLogic: isSignalMode ? "all" : null,
      entryConditions: isSignalMode ? this._mapConditions(conditions) : null,
      exit: {
        takeProfit: {
          enabled: !!takeProfit["enabled"],
          type: "fixed_percent",
          value: takeProfit["enabled"] ? this._toNullableNumber(takeProfit["value"]) : null,
          lookback: null,
        },
        stopLoss: {
          enabled: !!stopLoss["enabled"],
          type: "fixed_percent",
          value: stopLoss["enabled"] ? this._toNullableNumber(stopLoss["value"]) : null,
          lookback: null,
        },
        exitOnOppositeSignal: !!exit["exitOnOppositeSignal"],
      },
      risk: {
        positionSizeType: (risk["positionSizeType"] as PositionSizeType | undefined) ?? "percent_wallet",
        positionSizeValue: Number(risk["positionSizeValue"] ?? 0),
        leverage: Number(risk["leverage"] ?? 1),
        maxOpenTrades: Number(risk["maxOpenTrades"] ?? 1),
        cooldownValue: Number(risk["cooldownValue"] ?? 0),
        cooldownUnit: risk["cooldownUnit"] === "minutes" ? "minutes" : "candles",
        allowSameCandleReentry: !!risk["allowSameCandleReentry"],
      },
      metadata: {
        tags: Array.isArray(metadata["tags"]) ? metadata["tags"].map((tag) => String(tag)) : [],
        notes: String(metadata["notes"] ?? ""),
      },
      source: {
        entryPoint: "ui_builder",
        summary: "Created in strategy builder",
      },
    };
  }

  private _mapConditions(conditions: Record<string, unknown>[]): EntryConditionConfig[] {
    return conditions.map((condition) => ({
      id: String(condition["id"] ?? ""),
      enabled: Boolean(condition["enabled"] ?? true),
      type: String(condition["type"] ?? "rsi") as EntryConditionType,
      label: String(condition["label"] ?? ""),
      params: {
        period: Number(condition["period"] ?? 14),
        operator: String(condition["operator"] ?? "lt") as RsiOperator,
        value: Number(condition["value"] ?? 40),
      },
    }));
  }

  private _toNullableNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === "") {
      return null;
    }

    const parsedValue = Number(value);
    return Number.isNaN(parsedValue) ? null : parsedValue;
  }
}