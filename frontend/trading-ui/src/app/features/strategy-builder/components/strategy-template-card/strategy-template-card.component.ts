import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatIconModule } from "@angular/material/icon";
import { StrategyTemplateDto } from "../../models/strategy.model";

@Component({
  selector: "app-strategy-template-card",
  standalone: true,
  imports: [MatButtonModule, MatCardModule, MatChipsModule, MatIconModule],
  templateUrl: "./strategy-template-card.component.html",
  styleUrl: "./strategy-template-card.component.scss"
})
export class StrategyTemplateCardComponent {
  @Input({ required: true })
  public template!: StrategyTemplateDto;

  @Output()
  public readonly clone = new EventEmitter<StrategyTemplateDto>();

  public get modeIcon(): string {
    switch (this.template.strategyMode) {
      case "signal": return "show_chart";
      case "dca": return "stacked_line_chart";
      case "grid": return "grid_on";
      default: return "auto_graph";
    }
  }

  public onClone(): void {
    this.clone.emit(this.template);
  }
}
