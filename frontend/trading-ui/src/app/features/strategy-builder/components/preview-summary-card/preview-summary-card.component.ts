import { Component, Input, signal } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { RsiOperator } from "../../models/strategy.model";

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

    if (templateId === "custom_signal") {
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
    const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];
    const direction = String(formValue["direction"] ?? "long");
    const market = String(formValue["market"] ?? "market");
    const timeframe = String(formValue["timeframe"] ?? "timeframe");

    if (conditions.length === 0) {
      return "Add entry conditions to see a preview.";
    }

    const conditionTexts = conditions
      .filter((condition) => Boolean(condition["enabled"] ?? true))
      .map((condition) => {
        const period = Number(condition["period"] ?? 14);
        const operator = String(condition["operator"] ?? "lt") as RsiOperator;
        const value = Number(condition["value"] ?? 0);

        return `RSI(${period}) ${this._operatorText(operator)} ${value}`;
      });

    if (conditionTexts.length === 0) {
      return "All conditions are disabled.";
    }

    return `Enter a ${direction} trade on ${market} ${timeframe} when ${conditionTexts.join(" and ")}.`;
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

  private _formatNumber(value: unknown): string {
    const parsedValue = Number(value ?? 0);
    if (Number.isInteger(parsedValue)) {
      return parsedValue.toFixed(0);
    }

    return parsedValue.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
  }
}