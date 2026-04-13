import { Component, Input } from "@angular/core";
import { FormGroup } from "@angular/forms";
import { ExitRulesCardComponent } from "../../../components/exit-rules-card/exit-rules-card.component";

@Component({
  selector: "app-wizard-exit-step",
  standalone: true,
  imports: [ExitRulesCardComponent],
  templateUrl: "./wizard-exit-step.component.html",
  styleUrl: "./wizard-exit-step.component.scss"
})
export class WizardExitStepComponent {
  @Input({ required: true }) public group!: FormGroup;
}
