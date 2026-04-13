import { Component, Input } from "@angular/core";
import { FormGroup } from "@angular/forms";
import { RiskManagementCardComponent } from "../../../components/risk-management-card/risk-management-card.component";

@Component({
  selector: "app-wizard-risk-step",
  standalone: true,
  imports: [RiskManagementCardComponent],
  templateUrl: "./wizard-risk-step.component.html",
  styleUrl: "./wizard-risk-step.component.scss"
})
export class WizardRiskStepComponent {
  @Input({ required: true }) public group!: FormGroup;
}
