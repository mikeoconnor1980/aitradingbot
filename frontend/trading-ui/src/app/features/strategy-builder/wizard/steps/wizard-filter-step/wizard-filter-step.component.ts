import { Component, Input } from "@angular/core";
import { FormGroup } from "@angular/forms";
import { TrendFilterCardComponent } from "../../../components/trend-filter-card/trend-filter-card.component";

@Component({
  selector: "app-wizard-filter-step",
  standalone: true,
  imports: [TrendFilterCardComponent],
  templateUrl: "./wizard-filter-step.component.html",
  styleUrl: "./wizard-filter-step.component.scss"
})
export class WizardFilterStepComponent {
  @Input({ required: true }) public group!: FormGroup;
}
