import { Component, Input, signal } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MacdOperator, PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator, TrendOperator } from "../../models/strategy.model";

@Component({
  selector: "app-preview-summary-card",
  standalone: true,
  imports: [MatCardModule],
  templateUrl: "./preview-summary-card.component.html",
  styleUrl: "./preview-summary-card.component.scss"
})
export class PreviewSummaryCardComponent {
  private readonly _dayLabels = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

  @Input()
  public set formValue(value: Record<string, unknown> | null) {
    this._formValue.set(value);
  }

  private readonly _formValue = signal<Record<string, unknown> | null>(null);

  public get previewText(): string {
    const formValue = this._formValue();

    if (formValue === null) {
      return "Fill in the form to see a preview.";
    }

    const templateId = String(formValue["templateId"] ?? "grid");
    const strategyMode = String(formValue["strategyMode"] ?? "grid");

    if (strategyMode === "dca" || this._isDcaTemplate(templateId)) {
      return this._buildDcaPreview(formValue);
    }

    if (strategyMode === "signal" || this._isSignalTemplate(templateId)) {
      return this._buildSignalPreview(formValue);
    }

    const grid = (formValue["grid"] ?? null) as Record<string, unknown> | null;
    const exit = (formValue["exit"] ?? null) as Record<string, unknown> | null;
    const risk = (formValue["risk"] ?? null) as Record<string, unknown> | null;
    const takeProfit = (exit?.["takeProfit"] ?? null) as Record<string, unknown> | null;
    const stopLoss = (exit?.["stopLoss"] ?? null) as Record<string, unknown> | null;

    if (grid === null) {
      return "Fill in the form to see a preview.";
    }

    const parts: string[] = [];
    const direction = String(formValue["direction"] ?? "long");
    const market = String(formValue["market"] ?? "market");
    const timeframe = String(formValue["timeframe"] ?? "timeframe");
    const levels = Number(grid["levels"] ?? 0);
    const spacing = this._formatNumber(grid["spacing"]);
    const positionSize = this._formatNumber(risk?.["positionSizeValue"]);
    const leverage = this._formatNumber(risk?.["leverage"]);
    const takeProfitValue = this._formatNumber(takeProfit?.["value"]);
    const stopLossValue = this._formatNumber(stopLoss?.["value"]);

    parts.push(`Deploy a ${direction} grid on ${market} ${timeframe} with ${levels} levels at ${spacing}% spacing.`);

    if (Boolean(takeProfit?.["enabled"]) && Boolean(stopLoss?.["enabled"])) {
      parts.push(`Take profit at ${takeProfitValue}%, stop loss at ${stopLossValue}%.`);
    }

    if (risk !== null) {
      const positionSizeType = risk["positionSizeType"];

      if (positionSizeType === "risk_based") {
        const riskPercent = this._formatNumber(risk["riskPerTradePercent"] ?? 1);
        const leverageText = risk["autoLeverage"] ? "auto-leverage" : `${leverage}x leverage`;
        parts.push(`Risk: R-based ${riskPercent}% risk per trade, ${leverageText}.`);
      } else {
        parts.push(`Risk: ${positionSize}% of wallet, ${leverage}x leverage.`);
      }
    }

    return parts.join(" ");
  }

  private _buildSignalPreview(formValue: Record<string, unknown>): string {
    const conditions = this._getConditions(formValue);
    const direction = String(formValue["direction"] ?? "long");
    const market = String(formValue["market"] ?? "market");
    const timeframe = String(formValue["timeframe"] ?? "timeframe");
    const trendFilter = (formValue["trendFilter"] ?? null) as Record<string, unknown> | null;
    const parts: string[] = [`Signal strategy on ${market} ${timeframe} (${direction}).`];

    const trendFilterText = this._buildTrendFilterText(trendFilter);
    if (trendFilterText.length > 0) {
      parts.push(trendFilterText);
    }

    const conditionTexts = conditions
      .filter((condition) => Boolean(condition["enabled"] ?? true))
      .map((condition) => this._buildConditionText(condition))
      .filter((text) => text.length > 0);

    if (conditions.length === 0) {
      parts.push("Add entry conditions to see a preview.");
      return parts.join(" ");
    }

    if (conditionTexts.length === 0) {
      parts.push("All conditions are disabled.");
      return parts.join(" ");
    }

    const entryLogic = String(formValue["entryLogic"] ?? "all");
    const joiner = entryLogic === "any" ? " or " : " and ";
    parts.push(`Entry when ${conditionTexts.join(joiner)}.`);
    return parts.join(" ");
  }

  private _buildDcaPreview(formValue: Record<string, unknown>): string {
    const dca = (formValue["dca"] ?? null) as Record<string, unknown> | null;
    if (dca === null) {
      return "Configure the DCA schedule to see a preview.";
    }

    const market = String(formValue["market"] ?? "market");
    const rawInterval = String(dca["interval"] ?? "weekly");
    const interval = rawInterval.replace(/_/g, " ");
    const timeOfDayUtc = String(dca["timeOfDayUtc"] ?? "00:00");
    const baseAmountUsd = this._formatNumber(dca["baseAmountUsd"]);
    const gateConditions = (dca["gateConditions"] ?? null) as Record<string, unknown> | null;
    const scalingBands = Array.isArray(dca["scalingBands"]) ? dca["scalingBands"] as Record<string, unknown>[] : [];
    const scheduleText = rawInterval === "five_minutes"
      ? `every 5 minutes aligned to ${timeOfDayUtc} UTC`
      : `${interval} at ${timeOfDayUtc} UTC`;
    const parts = [`Spot DCA on ${market}: buy $${baseAmountUsd} ${scheduleText}.`];

    if (rawInterval === "weekly" || rawInterval === "biweekly") {
      parts.push(`Scheduled for ${this._dayOfWeekText(dca["dayOfWeek"])}.`);
    }

    if (rawInterval === "monthly") {
      parts.push(`Scheduled day of month: ${this._formatNumber(dca["dayOfMonth"])}.`);
    }

    const maxPriceUsd = this._toNullableNumber(gateConditions?.["maxPriceUsd"]);
    if (maxPriceUsd !== null) {
      parts.push(`Only buy at or below $${this._formatNumber(maxPriceUsd)}.`);
    }

    const minFearGreedIndex = this._toNullableNumber(gateConditions?.["minFearGreedIndex"]);
    const maxFearGreedIndex = this._toNullableNumber(gateConditions?.["maxFearGreedIndex"]);

    if (minFearGreedIndex !== null && maxFearGreedIndex !== null) {
      parts.push(`Fear & Greed must stay between ${this._formatNumber(minFearGreedIndex)} and ${this._formatNumber(maxFearGreedIndex)}.`);
    } else if (minFearGreedIndex !== null) {
      parts.push(`Fear & Greed must be ${this._formatNumber(minFearGreedIndex)} or higher.`);
    } else if (maxFearGreedIndex !== null) {
      parts.push(`Fear & Greed must be ${this._formatNumber(maxFearGreedIndex)} or lower.`);
    }

    if (scalingBands.length > 0) {
      const bandText = scalingBands.map((band) => {
        const lower = this._toNullableNumber(band["priceLowerUsd"]);
        const upper = this._toNullableNumber(band["priceUpperUsd"]);
        const scaling = this._formatNumber(band["scalingPercent"]);
        const lowerText = lower === null ? "open" : `$${this._formatNumber(lower)}`;
        const upperText = upper === null ? "open" : `$${this._formatNumber(upper)}`;
        return `${scaling}% between ${lowerText} and ${upperText}`;
      });

      parts.push(`Scaling bands: ${bandText.join(", ")}.`);
    }

    return parts.join(" ");
  }

  private _operatorText(operator: RsiOperator): string {
    const operatorMap: Record<RsiOperator, string> = {
      lt: "is below",
      lte: "is at or below",
      gt: "is above",
      gte: "is at or above",
      cross_above: "crosses above",
      cross_below: "crosses below",
    };

    return operatorMap[operator] ?? operator;
  }

  private _buildTrendFilterText(trendFilter: Record<string, unknown> | null): string {
    if (trendFilter === null || !(trendFilter["enabled"] ?? false)) {
      return "";
    }

    const type = String(trendFilter["type"] ?? "ema_cross");
    if (type === "ema_cross" || type === "sma_cross") {
      const maType = type === "ema_cross" ? "EMA" : "SMA";
      const fast = Number(trendFilter["fastPeriod"] ?? 0);
      const slow = Number(trendFilter["slowPeriod"] ?? 0);
      const operator = String(trendFilter["operator"] ?? "gt") as TrendOperator;
      return `When the ${fast} ${maType} ${this._trendOperatorText(operator)} the ${slow} ${maType}.`;
    }

    if (type === "price_above_ema") {
      const period = Number(trendFilter["period"] ?? 0);
      const operator = String(trendFilter["operator"] ?? "above") as TrendOperator;
      return `When price ${this._priceTrendOperatorText(operator)} the ${period} EMA.`;
    }

    return "";
  }

  private _buildConditionText(condition: Record<string, unknown>): string {
    const type = String(condition["type"] ?? "rsi");

    if (type === "candle_pattern") {
      const pattern = String(condition["pattern"] ?? "bullish_engulfing");
      const patternMap: Record<string, string> = {
        bullish_engulfing: "bullish engulfing candle appears",
        bearish_engulfing: "bearish engulfing candle appears",
        bullish_rejection: "bullish rejection candle appears",
        bearish_rejection: "bearish rejection candle appears",
        bullish_continuation: "bullish continuation candle appears",
        bearish_continuation: "bearish continuation candle appears",
        bullish_rejection_or_engulfing: "bullish rejection or engulfing candle appears",
        bearish_rejection_or_engulfing: "bearish rejection or engulfing candle appears",
      };

      return patternMap[pattern] ?? pattern;
    }

    if (type === "liquidity_sweep") {
      const lookbackBars = Number(condition["lookbackBars"] ?? 50);
      const pivotBars = Number(condition["pivotBars"] ?? 2);
      const side = String(condition["side"] ?? "upside");
      return `${side} liquidity sweep over ${lookbackBars} bars using ${pivotBars}-bar pivots`;
    }

    if (type === "structure_shift") {
      const pivotBars = Number(condition["pivotBars"] ?? 2);
      const direction = String(condition["direction"] ?? "bullish");
      return `${direction} structure shift using ${pivotBars}-bar pivots`;
    }

    if (type === "price_vs_ema") {
      const period = Number(condition["period"] ?? 50);
      const operator = String(condition["operator"] ?? "near") as PriceVsEmaOperator;

      if (operator === "near") {
        const distanceValue = this._formatNumber(condition["distanceValue"]);
        const distanceType = String(condition["distanceType"] ?? "percent") as PriceVsEmaDistanceType;
        const distanceUnit = distanceType === "percent" ? "%" : distanceType === "absolute" ? " points" : " ATR";
        return `price is within ${distanceValue}${distanceUnit} of the ${period} EMA`;
      }

      if (operator === "touch") {
        return `price touches the ${period} EMA`;
      }

      return `price ${this._priceConditionOperatorText(operator)} the ${period} EMA`;
    }

    if (type === "macd") {
      return this._buildMacdConditionText(condition);
    }

    if (type === "support_resistance") {
      return this._buildSupportResistanceConditionText(condition);
    }

    const period = Number(condition["period"] ?? 14);
    const operator = String(condition["operator"] ?? "lt") as RsiOperator;
    const value = this._formatNumber(condition["value"]);
    return `RSI(${period}) ${this._operatorText(operator)} ${value}`;
  }

  private _trendOperatorText(operator: TrendOperator): string {
    const operatorMap: Partial<Record<TrendOperator, string>> = {
      gt: "is above",
      gte: "is at or above",
      lt: "is below",
      lte: "is at or below",
      cross_above: "crosses above",
      cross_below: "crosses below",
    };

    return operatorMap[operator] ?? operator;
  }

  private _priceTrendOperatorText(operator: TrendOperator): string {
    const operatorMap: Partial<Record<TrendOperator, string>> = {
      above: "is above",
      below: "is below",
      cross_above: "crosses above",
      cross_below: "crosses below",
    };

    return operatorMap[operator] ?? operator;
  }

  private _priceConditionOperatorText(operator: PriceVsEmaOperator): string {
    const operatorMap: Record<Exclude<PriceVsEmaOperator, "near" | "touch">, string> = {
      above: "is above",
      below: "is below",
      cross_above: "crosses above",
      cross_below: "crosses below",
    };

    return operatorMap[operator as Exclude<PriceVsEmaOperator, "near" | "touch">] ?? operator;
  }

  private _buildMacdConditionText(condition: Record<string, unknown>): string {
    const fast = Number(condition["fastPeriod"] ?? 12);
    const slow = Number(condition["slowPeriod"] ?? 26);
    const signal = Number(condition["signalPeriod"] ?? 9);
    const operator = String(condition["operator"] ?? "cross_above_signal") as MacdOperator;
    const operatorMap: Record<MacdOperator, string> = {
      cross_above_signal: "crosses above signal line",
      cross_below_signal: "crosses below signal line",
      above_zero: "is above zero",
      below_zero: "is below zero",
      histogram_rising: "histogram is rising",
      histogram_falling: "histogram is falling",
    };

    return `MACD(${fast},${slow},${signal}) ${operatorMap[operator] ?? operator}`;
  }

  private _buildSupportResistanceConditionText(condition: Record<string, unknown>): string {
    const lookback = Number(condition["lookback"] ?? 50);
    const operator = String(condition["operator"] ?? "near_support");
    const tolerance = Number(condition["tolerance"] ?? 0.5);
    const operatorMap: Record<string, string> = {
      near_support: `near support (±${tolerance}%)`,
      near_resistance: `near resistance (±${tolerance}%)`,
      above_support: "above support",
      below_resistance: "below resistance",
      bounce_support: `bounce off support (±${tolerance}%)`,
      bounce_resistance: `bounce off resistance (±${tolerance}%)`,
    };

    return `S/R(${lookback}) ${operatorMap[operator] ?? operator}`;
  }

  private _getConditions(formValue: Record<string, unknown>): Record<string, unknown>[] {
    const conditions = formValue["conditions"];
    if (Array.isArray(conditions)) {
      return conditions as Record<string, unknown>[];
    }

    const entryConditions = formValue["entryConditions"];
    return Array.isArray(entryConditions) ? entryConditions as Record<string, unknown>[] : [];
  }

  private _isSignalTemplate(templateId: string): boolean {
    return templateId === "custom_signal" || templateId === "ema_pullback" || templateId === "macd_cross";
  }

  private _isDcaTemplate(templateId: string): boolean {
    return templateId === "dca";
  }

  private _toNullableNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === "") {
      return null;
    }

    const parsedValue = Number(value);
    return Number.isNaN(parsedValue) ? null : parsedValue;
  }

  private _dayOfWeekText(value: unknown): string {
    const dayIndex = this._toNullableNumber(value);

    if (dayIndex === null || dayIndex < 0 || dayIndex > 6) {
      return "the selected weekday";
    }

    return this._dayLabels[dayIndex] ?? "the selected weekday";
  }

  private _formatNumber(value: unknown): string {
    const parsedValue = Number(value ?? 0);
    if (Number.isInteger(parsedValue)) {
      return parsedValue.toFixed(0);
    }

    return parsedValue.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
  }
}