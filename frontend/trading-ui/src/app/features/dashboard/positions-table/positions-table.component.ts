import { DecimalPipe, NgClass } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { Position } from "../../../core/models/position.model";

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [DecimalPipe, NgClass, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: "./positions-table.component.html",
  styleUrl: "./positions-table.component.scss"
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  @Output()
  public closePosition = new EventEmitter<Position>();

  public readonly loadingPositionKeys = new Set<string>();

  public getPositionKey(position: Position): string {
    return position.asset + position.side;
  }

  public isLoading(position: Position): boolean {
    return this.loadingPositionKeys.has(this.getPositionKey(position));
  }

  public setLoading(key: string, loading: boolean): void {
    if (loading) {
      this.loadingPositionKeys.add(key);
      return;
    }

    this.loadingPositionKeys.delete(key);
  }

  public onCloseClick(position: Position): void {
    if (this.isLoading(position)) {
      return;
    }

    this.closePosition.emit(position);
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "pnl--profit" : "pnl--loss";
  }
}