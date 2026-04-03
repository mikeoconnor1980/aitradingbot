import { DecimalPipe } from "@angular/common";
import { Component, Input, inject } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { Router } from "@angular/router";
import { BacktestResult } from "../../../core/models/backtest.model";

@Component({
  selector: "app-backtest-result",
  standalone: true,
  imports: [DecimalPipe, MatCardModule, MatIconModule],
  templateUrl: "./backtest-result.component.html",
  styleUrl: "./backtest-result.component.scss"
})
export class BacktestResultComponent {
  private readonly _router = inject(Router);

  @Input({ required: true })
  public result!: BacktestResult;

  public get hasNoTrades(): boolean {
    return this.result.totalTrades === 0;
  }

  public get totalPnlClass(): string {
    return this.getPnlClass(this.result.totalPnl);
  }

  public get averageTradePnlClass(): string {
    return this.getPnlClass(this.result.averageTradePnl);
  }

  public get drawdownPercent(): number {
    const timeSeries = this.result.equityTimeSeries;

    if (timeSeries !== undefined && timeSeries !== null && timeSeries.length > 1) {
      let peak = timeSeries[0].equity;
      let maxDrawdownPercent = 0;

      for (const snapshot of timeSeries) {
        peak = Math.max(peak, snapshot.equity);
        const drawdownPercent = peak > 0 ? ((snapshot.equity - peak) / peak) * 100 : 0;
        maxDrawdownPercent = Math.min(maxDrawdownPercent, drawdownPercent);
      }

      return maxDrawdownPercent;
    }

    if (this.result.initialCapital <= 0) {
      return 0;
    }

    return (this.result.maxDrawdown / this.result.initialCapital) * 100;
  }

  public get averageHoldTimeLabel(): string {
    return this._formatDurationFromMinutes(this.result.averageHoldTimeMinutes);
  }

  public get elapsedLabel(): string {
    return this._formatDurationFromMilliseconds(this.result.elapsedMs);
  }

  public get intervalsLabel(): string {
    return this.result.intervals.join(", ");
  }

  public get canNavigateToStrategy(): boolean {
    return this.result.strategyId !== null
      && this.result.strategyId !== undefined
      && this.result.strategyName !== null
      && this.result.strategyName !== undefined
      && !this.isDeletedStrategy(this.result.strategyName);
  }

  public get entryModeLabel(): string {
    switch (this.result.strategyConfig.grid?.entryMode) {
      case "WaitForLimitPrice":
        return "Wait for limit price";
      case "InitialMarketThenGrid":
        return "Initial market buy, then grid";
      default:
        return "Auto from signal candle";
    }
  }

  public get limitPriceLabel(): string {
    const anchorPrice = this.result.strategyConfig.grid?.anchorPrice;

    return this.result.strategyConfig.grid?.entryMode === "WaitForLimitPrice" && anchorPrice !== null && anchorPrice !== undefined
      ? `$${anchorPrice.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : "—";
  }

  public get gridLevels(): number {
    return this.result.strategyConfig.grid?.levels ?? 0;
  }

  public get gridSpacing(): number {
    return this.result.strategyConfig.grid?.spacing ?? 0;
  }

  public get takeProfitPercent(): number {
    return this.result.strategyConfig.exit.takeProfit.value ?? 0;
  }

  public get positionSize(): number {
    return this.result.strategyConfig.risk.positionSizeValue;
  }

  public get leverage(): number {
    return this.result.strategyConfig.risk.leverage ?? this.result.executionConfig.leverage ?? 1;
  }

  public get stopLossPercent(): number {
    return this.result.strategyConfig.exit.stopLoss.value ?? 0;
  }

  public onNavigateToStrategy(strategyId: string): void {
    void this._router.navigate(["/strategies", strategyId, "edit"]);
  }

  public isDeletedStrategy(strategyName: string | null | undefined): boolean {
    return strategyName?.endsWith(" (deleted)") ?? false;
  }

  public getPnlClass(value: number): string {
    return value >= 0 ? "backtest-result__value--profit" : "backtest-result__value--loss";
  }

  private _formatDurationFromMinutes(totalMinutes: number): string {
    const roundedMinutes = Math.max(0, Math.round(totalMinutes));

    if (roundedMinutes < 60) {
      return `${roundedMinutes}m`;
    }

    const days = Math.floor(roundedMinutes / (24 * 60));
    const hours = Math.floor((roundedMinutes % (24 * 60)) / 60);
    const minutes = roundedMinutes % 60;
    const parts: string[] = [];

    if (days > 0) {
      parts.push(`${days}d`);
    }

    if (hours > 0) {
      parts.push(`${hours}h`);
    }

    if (minutes > 0 || parts.length === 0) {
      parts.push(`${minutes}m`);
    }

    return parts.join(" ");
  }

  private _formatDurationFromMilliseconds(totalMilliseconds: number): string {
    if (totalMilliseconds < 1000) {
      return `${totalMilliseconds}ms`;
    }

    const totalSeconds = Math.round(totalMilliseconds / 1000);

    if (totalSeconds < 60) {
      return `${totalSeconds}s`;
    }

    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;

    if (minutes < 60) {
      return seconds > 0 ? `${minutes}m ${seconds}s` : `${minutes}m`;
    }

    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    const parts = [`${hours}h`];

    if (remainingMinutes > 0) {
      parts.push(`${remainingMinutes}m`);
    }

    return parts.join(" ");
  }
}