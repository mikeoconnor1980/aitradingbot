import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output, computed, input } from "@angular/core";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { OptimizationResult } from "../../../core/models/optimizer.model";

@Component({
  selector: "app-optimizer-results-table",
  standalone: true,
  imports: [DecimalPipe, MatTableModule, MatTooltipModule],
  templateUrl: "./optimizer-results-table.component.html",
  styleUrl: "./optimizer-results-table.component.scss"
})
export class OptimizerResultsTableComponent {
  public readonly results = input.required<OptimizationResult[]>();

  @Input()
  public selectedRank: number | null = null;

  @Output()
  public selectResult = new EventEmitter<OptimizationResult>();

  private static readonly coreColumns = ["rank", "signalDescription", "fitnessScore", "totalPnl", "winRate", "maxDrawdown", "totalTrades", "sharpeRatio", "profitFactor"];
  private static readonly oosColumns = ["oosFitnessScore", "oosTotalPnl"];

  public readonly displayedColumns = computed(() => {
    const hasOos = this.results().some(r => r.oosFitnessScore != null);
    return hasOos
      ? [...OptimizerResultsTableComponent.coreColumns, ...OptimizerResultsTableComponent.oosColumns]
      : OptimizerResultsTableComponent.coreColumns;
  });

  public onSelect(result: OptimizationResult): void {
    this.selectResult.emit(result);
  }

  public getPnlClass(value: number): string {
    return value >= 0 ? "optimizer-results-table__value--profit" : "optimizer-results-table__value--loss";
  }
}