import { DecimalPipe, NgClass } from "@angular/common";
import { Component, inject, Input, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { LayoutService } from "../../../core/services/layout.service";
import { AccountSummary } from "../../../core/models/account-summary.model";
import { MarginRatioIndicatorComponent } from "./margin-ratio-indicator/margin-ratio-indicator.component";

@Component({
  selector: "app-account-summary",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatButtonModule, MatCardModule, MatIconModule, MarginRatioIndicatorComponent],
  templateUrl: "./account-summary.component.html",
  styleUrl: "./account-summary.component.scss"
})
export class AccountSummaryComponent {
  private readonly _layout = inject(LayoutService);

  public readonly isMobile = this._layout.isMobile;
  public readonly expanded = signal(false);

  @Input({ required: true })
  public summary!: AccountSummary;

  public get pnlClass(): string {
    return this.summary.unrealisedPnl >= 0 ? "pnl--profit" : "pnl--loss";
  }

  public toggleExpanded(): void {
    this.expanded.update((v) => !v);
  }
}