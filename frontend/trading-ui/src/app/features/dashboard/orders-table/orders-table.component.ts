import { DecimalPipe, NgClass } from "@angular/common";
import { Component, Input } from "@angular/core";
import { OpenOrder } from "../../../core/models/open-order.model";

@Component({
  selector: "app-orders-table",
  standalone: true,
  imports: [DecimalPipe, NgClass],
  templateUrl: "./orders-table.component.html",
  styleUrl: "./orders-table.component.scss"
})
export class OrdersTableComponent {
  @Input()
  public orders: OpenOrder[] = [];

  public getSideClass(side: string): string {
    return side.toLowerCase() === "buy" ? "side--buy" : "side--sell";
  }
}