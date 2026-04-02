import { Component, Input, signal } from "@angular/core";
import { MatCardModule } from "@angular/material/card";

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

  private _formatNumber(value: unknown): string {
    const parsedValue = Number(value ?? 0);
    if (Number.isInteger(parsedValue)) {
      return parsedValue.toFixed(0);
    }

    return parsedValue.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
  }
}