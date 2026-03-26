import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatTooltipModule } from "@angular/material/tooltip";

@Component({
  selector: "app-funding-indicator",
  standalone: true,
  imports: [DecimalPipe, MatTooltipModule],
  templateUrl: "./funding-indicator.component.html",
  styleUrl: "./funding-indicator.component.scss"
})
export class FundingIndicatorComponent {
  @Input()
  public fundingRate = 0;

  @Input()
  public side = "";

  @Input()
  public notional = 0;

  public get fundingClass(): string {
    if (this.fundingRate === 0) {
      return "";
    }

    return this.isReceiving ? "funding-indicator--receiving" : "funding-indicator--paying";
  }

  public get ratePercent(): number {
    return this.fundingRate * 100;
  }

  public get tooltipText(): string {
    if (this.fundingRate === 0) {
      return "";
    }

    const hourlyPercent = this.ratePercent.toFixed(4);
    const dailyEstimate = Math.abs(this.fundingRate) * this.notional * 24;
    const sign = this.isReceiving ? "+" : "-";

    return `Hourly: ${hourlyPercent}% | Est. daily: ${sign}$${dailyEstimate.toFixed(2)}`;
  }

  private get isReceiving(): boolean {
    return this.side === "Long"
      ? this.fundingRate < 0
      : this.fundingRate > 0;
  }
}