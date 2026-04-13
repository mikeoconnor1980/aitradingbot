import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { PreviewSummaryCardComponent } from "../../../components/preview-summary-card/preview-summary-card.component";
import { ValidationCardComponent } from "../../../components/validation-card/validation-card.component";
import { ValidationError } from "../../../models/strategy.model";

@Component({
  selector: "app-wizard-review-step",
  standalone: true,
  imports: [MatButtonModule, MatIconModule, PreviewSummaryCardComponent, ValidationCardComponent],
  templateUrl: "./wizard-review-step.component.html",
  styleUrl: "./wizard-review-step.component.scss"
})
export class WizardReviewStepComponent {
  @Input() public formValue: Record<string, unknown> | null = null;
  @Input() public errors: ValidationError[] = [];
  @Input() public warnings: ValidationError[] = [];
  @Input() public isSaving = false;

  @Output() public readonly save = new EventEmitter<void>();
  @Output() public readonly switchToBuilder = new EventEmitter<void>();

  public get canSave(): boolean {
    return this.errors.length === 0 && !this.isSaving;
  }
}
