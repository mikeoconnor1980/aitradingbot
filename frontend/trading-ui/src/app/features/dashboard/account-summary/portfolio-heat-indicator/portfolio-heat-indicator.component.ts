import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { PortfolioHeatPosition } from "../../../../core/models/portfolio-heat.model";

interface HeatThresholdConfig {
  readonly cssClass: "low" | "elevated" | "critical";
  readonly label: string;
}

@Component({
  selector: "app-portfolio-heat-indicator",
  standalone: true,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: "./portfolio-heat-indicator.component.html",
  styleUrl: "./portfolio-heat-indicator.component.scss"
})
export class PortfolioHeatIndicatorComponent {
  @Input({ required: true })
  public heatPercent!: number;

  @Input({ required: true })
  public maxHeatPercent!: number;

  @Input()
  public positions: PortfolioHeatPosition[] = [];

  public get barValue(): number {
    if (this.maxHeatPercent <= 0) {
      return 0;
    }

    return Math.min(Math.max((this.heatPercent / this.maxHeatPercent) * 100, 0), 100);
  }

  public get threshold(): HeatThresholdConfig {
    if (this.maxHeatPercent <= 0) {
      return { cssClass: "low", label: "Heat limit disabled" };
    }

    const ratio = this.heatPercent / this.maxHeatPercent;
    if (ratio > 0.8) {
      return { cssClass: "critical", label: "Critical - near heat limit" };
    }

    if (ratio >= 0.5) {
      return { cssClass: "elevated", label: "Elevated heat" };
    }

    return { cssClass: "low", label: "Low heat" };
  }

  public get isCritical(): boolean {
    return this.maxHeatPercent > 0 && (this.heatPercent / this.maxHeatPercent) > 0.8;
  }

  public get tooltipText(): string {
    if (this.maxHeatPercent <= 0) {
      return "Portfolio heat limit is disabled.";
    }

    if (this.positions.length === 0) {
      return `${this.threshold.label}\nNo open positions`;
    }

    const breakdown = this.positions
      .map((position) => `${position.symbol}: $${position.riskUsd.toFixed(2)} (${position.riskPercent.toFixed(1)}%)`)
      .join("\n");

    return `${this.threshold.label}\n${breakdown}`;
  }
}