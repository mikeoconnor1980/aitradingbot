import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatTableModule } from "@angular/material/table";
import { OptimizationResult } from "../../../core/models/optimizer.model";

@Component({
  selector: "app-optimizer-results-table",
  standalone: true,
  imports: [DecimalPipe, MatTableModule],
  templateUrl: "./optimizer-results-table.component.html",
  styleUrl: "./optimizer-results-table.component.scss"
})
export class OptimizerResultsTableComponent {
  @Input({ required: true })
  public results: OptimizationResult[] = [];

  @Input()
  public selectedRank: number | null = null;

  @Output()
  public selectResult = new EventEmitter<OptimizationResult>();

  public readonly displayedColumns = ["rank", "signalDescription", "fitnessScore", "totalPnl", "winRate", "maxDrawdown", "totalTrades"];

  public onSelect(result: OptimizationResult): void {
    this.selectResult.emit(result);
  }

  public getPnlClass(value: number): string {
    return value >= 0 ? "optimizer-results-table__value--profit" : "optimizer-results-table__value--loss";
  }
}