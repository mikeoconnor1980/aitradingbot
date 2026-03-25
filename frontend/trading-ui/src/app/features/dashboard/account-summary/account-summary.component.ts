import { DecimalPipe, NgClass } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { AccountSummary } from "../../../core/models/account-summary.model";

@Component({
  selector: "app-account-summary",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatCardModule],
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