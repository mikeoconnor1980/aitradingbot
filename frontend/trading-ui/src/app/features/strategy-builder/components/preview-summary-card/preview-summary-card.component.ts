import { Component, Input, signal } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator, TrendOperator } from "../../models/strategy.model";

@Component({
  selector: "app-preview-summary-card",
  standalone: true,
  imports: [MatCardModule],
  templateUrl: "./preview-summary-card.component.html",
  styleUrl: "./preview-summary-card.component.scss"
})
export class PreviewSummaryCardComponent {
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
      parts.push(`Risk: ${positionSize}% of wallet, ${leverage}x leverage.`);
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

    parts.push(`Entry when ${conditionTexts.join(" and ")}.`);
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

  private _formatNumber(value: unknown): string {
    const parsedValue = Number(value ?? 0);
    if (Number.isInteger(parsedValue)) {
      return parsedValue.toFixed(0);
    }

    return parsedValue.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
  }
}