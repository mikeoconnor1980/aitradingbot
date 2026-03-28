import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { BacktestResult } from "../../../core/models/backtest.model";

@Component({
  selector: "app-backtest-result",
  standalone: true,
  imports: [DecimalPipe, MatCardModule, MatIconModule],
  templateUrl: "./backtest-result.component.html",
  styleUrl: "./backtest-result.component.scss"
})
export class BacktestResultComponent {
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
    if ((this.result.equityTimeSeries?.length ?? 0) > 1) {
      let peak = this.result.equityTimeSeries![0].equity;
      let maxDrawdownPercent = 0;

      for (const snapshot of this.result.equityTimeSeries!) {
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