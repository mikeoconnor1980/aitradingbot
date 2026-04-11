import { DecimalPipe, NgClass } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { OpenOrder } from "../../../../core/models/open-order.model";

@Component({
  selector: "app-order-card",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatButtonModule, MatIconModule],
  templateUrl: "./order-card.component.html",
  styleUrl: "./order-card.component.scss"
})
export class OrderCardComponent {
  @Input()
  public order!: OpenOrder;

  @Input()
  public loading = false;

  @Output()
  public cancelOrder = new EventEmitter<OpenOrder>();

  @Output()
  public modifyOrder = new EventEmitter<OpenOrder>();

  public get sideClass(): string {
    return this.order.side.toLowerCase() === "buy" ? "side--buy" : "side--sell";
  }

  public onCancel(): void {
    if (!this.loading) {
      this.cancelOrder.emit(this.order);
    }
  }

  public onModify(): void {
    if (!this.loading) {
      this.modifyOrder.emit(this.order);
    }
  }
}
