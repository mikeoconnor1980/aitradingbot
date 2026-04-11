import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { Position } from "../../../../core/models/position.model";

@Component({
  selector: "app-position-card",
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule],
  templateUrl: "./position-card.component.html",
  styleUrl: "./position-card.component.scss"
})
export class PositionCardComponent {
  @Input()
  public position!: Position;

  @Input()
  public equity = 0;

  @Input()
  public loading = false;

  @Output()
  public closePosition = new EventEmitter<Position>();

  @Output()
  public setSlTp = new EventEmitter<Position>();

  @Output()
  public removeSlTp = new EventEmitter<{ position: Position; field: "sl" | "tp" }>();

  @Output()
  public toggleDetails = new EventEmitter<Position>();

  public expanded = false;

  public get pnlClass(): string {
    return this.position.unrealisedPnl >= 0 ? "pnl--profit" : "pnl--loss";
  }

  public get sideClass(): string {
    return this.position.side === "Long" ? "side--long" : "side--short";
  }

  public get hasSlTp(): boolean {
    return this.position.stopLossPrice != null || this.position.takeProfitPrice != null;
  }

  public get marginLabel(): string {
    if (this.position.marginUsed <= 0) {
      return "—";
    }

    return `$${this.position.marginUsed.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  public get marginPercentLabel(): string {
    if (this.position.marginUsed <= 0 || this.equity <= 0) {
      return "—";
    }

    return `${((this.position.marginUsed / this.equity) * 100).toFixed(1)}%`;
  }

  public get liquidationLabel(): string {
    if (this.position.liquidationPrice <= 0) {
      return "—";
    }

    return this.position.liquidationPrice.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  public onToggleExpand(): void {
    this.expanded = !this.expanded;
  }

  public onClose(): void {
    if (!this.loading) {
      this.closePosition.emit(this.position);
    }
  }

  public onSetSlTp(): void {
    if (!this.loading) {
      this.setSlTp.emit(this.position);
    }
  }

  public onRemoveSl(): void {
    if (!this.loading) {
      this.removeSlTp.emit({ position: this.position, field: "sl" });
    }
  }

  public onRemoveTp(): void {
    if (!this.loading) {
      this.removeSlTp.emit({ position: this.position, field: "tp" });
    }
  }
}
