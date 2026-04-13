import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { TemplateEducation, WizardEducationService } from "../../services/wizard-education.service";

@Component({
  selector: "app-wizard-goal-step",
  standalone: true,
  imports: [MatCardModule, MatIconModule],
  templateUrl: "./wizard-goal-step.component.html",
  styleUrl: "./wizard-goal-step.component.scss"
})
export class WizardGoalStepComponent {
  @Input() public selectedTemplateId = "grid";
  @Output() public readonly templateSelected = new EventEmitter<string>();

  public readonly templates: TemplateEducation[];

  public constructor() {
    const education = new WizardEducationService();
    this.templates = education.templates;
  }

  public selectTemplate(template: TemplateEducation): void {
    if (!template.available) {
      return;
    }

    this.templateSelected.emit(template.id);
  }
}
