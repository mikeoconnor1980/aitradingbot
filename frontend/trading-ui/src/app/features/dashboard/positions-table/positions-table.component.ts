import { DecimalPipe, NgClass } from "@angular/common";
import { Component, Input } from "@angular/core";
import { Position } from "../../../core/models/position.model";

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [DecimalPipe, NgClass],
  templateUrl: "./positions-table.component.html",
  styleUrl: "./positions-table.component.scss"
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "pnl--profit" : "pnl--loss";
  }
}