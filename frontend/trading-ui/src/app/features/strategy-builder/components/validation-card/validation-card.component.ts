import { Component, Input } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { ValidationError } from "../../models/strategy.model";

@Component({
  selector: "app-validation-card",
  standalone: true,
  imports: [MatCardModule, MatIconModule],
  templateUrl: "./validation-card.component.html",
  styleUrl: "./validation-card.component.scss"
})
export class ValidationCardComponent {
  @Input() public errors: ValidationError[] = [];
  @Input() public warnings: ValidationError[] = [];
  @Input() public infoMessages: ValidationError[] = [];

  public get hasIssues(): boolean {
    return this.errors.length > 0 || this.warnings.length > 0 || this.infoMessages.length > 0;
  }
}