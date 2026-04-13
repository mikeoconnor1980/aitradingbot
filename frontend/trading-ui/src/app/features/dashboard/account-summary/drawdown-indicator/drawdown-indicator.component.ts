import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { DrawdownState } from "../../../../core/models/drawdown-state.model";

interface DrawdownThresholdConfig {
  readonly cssClass: "low" | "elevated" | "critical" | "halted";
  readonly label: string;
}

@Component({
  selector: "app-drawdown-indicator",
  standalone: true,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule],
  templateUrl: "./drawdown-indicator.component.html",
  styleUrl: "./drawdown-indicator.component.scss"
})
export class DrawdownIndicatorComponent {
  @Input()
  public drawdownState: DrawdownState | null = null;

  public get barValue(): number {
    if (!this.drawdownState) {
      return 0;
    }

    return Math.min(Math.max((this.drawdownState.drawdownPercent / 20) * 100, 0), 100);
  }

  public get threshold(): DrawdownThresholdConfig {
    if (!this.drawdownState) {
      return { cssClass: "low", label: "No data" };
    }

    if (this.drawdownState.isCircuitBreakerActive) {
      return { cssClass: "halted", label: "Halted" };
    }

    if (this.drawdownState.drawdownPercent >= 10) {
      return { cssClass: "critical", label: this.riskLabel };
    }

    if (this.drawdownState.drawdownPercent >= 5) {
      return { cssClass: "elevated", label: this.riskLabel };
    }

    return { cssClass: "low", label: "Full risk" };
  }

  public get isHalted(): boolean {
    return this.drawdownState?.isCircuitBreakerActive ?? false;
  }

  public get riskLabel(): string {
    if (!this.drawdownState) {
      return "No data";
    }

    return `${(this.drawdownState.scalingFactor * 100).toFixed(0)}% risk`;
  }
}