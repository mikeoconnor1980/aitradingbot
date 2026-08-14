import { CommonModule } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { AnalystToolInvocation } from "../../core/models/analyst.model";

@Component({
  selector: "app-analyst-evidence-card",
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: "./analyst-evidence-card.component.html",
  styleUrl: "./analyst-evidence-card.component.scss"
})
export class AnalystEvidenceCardComponent {
  @Input({ required: true })
  public invocation!: AnalystToolInvocation;

  public get result(): Record<string, unknown> {
    return this.invocation.result ?? {};
  }

  public get title(): string {
    return this.invocation.toolName.replaceAll("_", " ");
  }

  public get isMarketAnalysis(): boolean {
    return this.invocation.toolName === "analyse_market" || this.invocation.toolName === "get_market_snapshot";
  }

  public get isMultiTimeframe(): boolean {
    return this.invocation.toolName === "analyse_market_multi_timeframe";
  }

  public get isStrategyEvidence(): boolean {
    return this.invocation.toolName.startsWith("get_strategy_evaluation");
  }

  public get isTradeEvidence(): boolean {
    return this.invocation.toolName === "get_trade" || this.invocation.toolName.includes("trade_analytics");
  }

  public get isBacktestExperiment(): boolean {
    return this.invocation.toolName === "run_backtest_experiment";
  }

  public get rules(): Record<string, unknown>[] {
    return this.getArray(this.result["rules"]);
  }

  public get candidates(): Record<string, unknown>[] {
    return this.getArray(this.result["candidates"]);
  }

  public get timeframeRows(): Record<string, unknown>[] {
    return this.getArray(this.result["timeframes"]);
  }

  public display(value: unknown): string {
    if (value === null || value === undefined || value === "") return "-";
    return String(value);
  }

  public property(value: unknown, key: string): unknown {
    return value && typeof value === "object" ? (value as Record<string, unknown>)[key] : undefined;
  }

  private getArray(value: unknown): Record<string, unknown>[] {
    return Array.isArray(value) ? value.filter((item): item is Record<string, unknown> => !!item && typeof item === "object") : [];
  }
}