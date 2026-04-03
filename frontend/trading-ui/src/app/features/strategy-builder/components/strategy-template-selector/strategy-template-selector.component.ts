import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { STRATEGY_TEMPLATES, StrategyTemplate } from "../../models/strategy.model";

@Component({
  selector: "app-strategy-template-selector",
  standalone: true,
  imports: [MatCardModule],
  templateUrl: "./strategy-template-selector.component.html",
  styleUrl: "./strategy-template-selector.component.scss"
})
export class StrategyTemplateSelectorComponent {
  @Input() public selectedTemplateId = "grid";
  @Output() public readonly templateSelected = new EventEmitter<string>();

  public readonly templates: StrategyTemplate[] = STRATEGY_TEMPLATES;

  public selectTemplate(template: StrategyTemplate): void {
    if (!template.available) {
      return;
    }

    this.templateSelected.emit(template.id);
  }
}