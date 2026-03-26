import { DecimalPipe, NgClass } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { AccountSummary } from "../../../core/models/account-summary.model";
import { MarginRatioIndicatorComponent } from "./margin-ratio-indicator/margin-ratio-indicator.component";

@Component({
  selector: "app-account-summary",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatCardModule, MarginRatioIndicatorComponent],
  templateUrl: "./account-summary.component.html",
  styleUrl: "./account-summary.component.scss"
})
export class AccountSummaryComponent {
  @Input({ required: true })
  public summary!: AccountSummary;

  public get pnlClass(): string {
    return this.summary.unrealisedPnl >= 0 ? "pnl--profit" : "pnl--loss";
  }
}