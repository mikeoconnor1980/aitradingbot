import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTooltipModule } from "@angular/material/tooltip";

interface ThresholdConfig {
  readonly cssClass: "low" | "moderate" | "elevated" | "critical";
  readonly label: string;
}

@Component({
  selector: "app-margin-ratio-indicator",
  standalone: true,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: "./margin-ratio-indicator.component.html",
  styleUrl: "./margin-ratio-indicator.component.scss"
})
export class MarginRatioIndicatorComponent {
  @Input({ required: true })
  public ratio!: number;

  public get percentage(): number {
    return Math.min(Math.max(this.ratio * 100, 0), 100);
  }

  public get threshold(): ThresholdConfig {
    if (this.ratio >= 0.8) {
      return { cssClass: "critical", label: "Critical — near liquidation" };
    }

    if (this.ratio >= 0.6) {
      return { cssClass: "elevated", label: "Elevated" };
    }

    if (this.ratio >= 0.3) {
      return { cssClass: "moderate", label: "Moderate" };
    }

    return { cssClass: "low", label: "Low risk" };
  }

  public get isCritical(): boolean {
    return this.ratio >= 0.8;
  }
}