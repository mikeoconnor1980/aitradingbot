import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatTooltipModule } from "@angular/material/tooltip";
import { DurationPipe } from "../../../core/pipes/duration.pipe";
import { OptimizationResult, OptimizationRun, parseOptimizationStrategyConfig } from "../../../core/models/optimizer.model";

@Component({
  selector: "app-optimizer-detail",
  standalone: true,
  imports: [DecimalPipe, DurationPipe, MatButtonModule, MatCardModule, MatTooltipModule],
  templateUrl: "./optimizer-detail.component.html",
  styleUrl: "./optimizer-detail.component.scss"
})
export class OptimizerDetailComponent {
  @Input({ required: true })
  public run!: OptimizationRun;

  @Input()
  public result: OptimizationResult | null = null;

  @Output()
  public createStrategy = new EventEmitter<OptimizationResult>();

  public get strategyConfig() {
    return this.result === null ? null : parseOptimizationStrategyConfig(this.result.strategyConfigJson);
  }

  public get canCreateStrategy(): boolean {
    return this.result !== null && this.strategyConfig !== null;
  }

  public onCreateStrategy(): void {
    if (this.result === null) {
      return;
    }

    this.createStrategy.emit(this.result);
  }
}