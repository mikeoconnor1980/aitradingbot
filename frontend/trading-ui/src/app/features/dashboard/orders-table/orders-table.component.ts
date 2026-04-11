import { DecimalPipe, NgClass } from "@angular/common";
import { Component, EventEmitter, inject, Input, Output, ViewChild } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatMenuModule, MatMenuTrigger } from "@angular/material/menu";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTooltipModule } from "@angular/material/tooltip";
import { LayoutService } from "../../../core/services/layout.service";
import { OpenOrder } from "../../../core/models/open-order.model";
import { OrderCardComponent } from "./order-card/order-card.component";

@Component({
  selector: "app-orders-table",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule, MatTooltipModule, OrderCardComponent],
  templateUrl: "./orders-table.component.html",
  styleUrl: "./orders-table.component.scss"
})
export class OrdersTableComponent {
  private readonly _layout = inject(LayoutService);

  public readonly isMobile = this._layout.isMobile;

  @ViewChild(MatMenuTrigger)
  public contextMenuTrigger?: MatMenuTrigger;

  @Input()
  public orders: OpenOrder[] = [];

  @Output()
  public cancelOrder = new EventEmitter<OpenOrder>();

  @Output()
  public cancelAllOrders = new EventEmitter<void>();

  @Output()
  public modifyOrder = new EventEmitter<OpenOrder>();

  public readonly loadingOrderIds = new Set<string>();
  public globalLoading = false;
  public contextMenuOrder: OpenOrder | null = null;
  public contextMenuPosition = { x: "0px", y: "0px" };

  public isLoading(orderId: string): boolean {
    return this.globalLoading || this.loadingOrderIds.has(orderId);
  }

  public setLoading(orderId: string, loading: boolean): void {
    if (loading) {
      this.loadingOrderIds.add(orderId);
      return;
    }

    this.loadingOrderIds.delete(orderId);
  }

  public setGlobalLoading(loading: boolean): void {
    this.globalLoading = loading;
  }

  public onCancelClick(order: OpenOrder): void {
    if (this.isLoading(order.orderId)) {
      return;
    }

    this.cancelOrder.emit(order);
  }

  public onModifyClick(order: OpenOrder): void {
    if (this.isLoading(order.orderId)) {
      return;
    }

    this.modifyOrder.emit(order);
  }

  public onCancelAllClick(): void {
    if (this.globalLoading || this.loadingOrderIds.size > 0 || this.orders.length === 0) {
      return;
    }

    this.cancelAllOrders.emit();
  }

  public onContextMenu(event: MouseEvent, order: OpenOrder): void {
    event.preventDefault();
    this.contextMenuOrder = order;
    this.contextMenuPosition = {
      x: `${event.clientX}px`,
      y: `${event.clientY}px`
    };
    this.contextMenuTrigger?.openMenu();
  }

  public clearContextMenu(): void {
    this.contextMenuOrder = null;
  }

  public getSideClass(side: string): string {
    return side.toLowerCase() === "buy" ? "side--buy" : "side--sell";
  }
}