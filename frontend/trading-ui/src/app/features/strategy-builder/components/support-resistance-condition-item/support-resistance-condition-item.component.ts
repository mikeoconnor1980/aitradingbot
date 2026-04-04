import { Component, EventEmitter, Input, Output } from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { SUPPORT_RESISTANCE_OPERATORS, SupportResistanceOperatorOption } from "../../enums/support-resistance-operator.enum";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-support-resistance-condition-item",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./support-resistance-condition-item.component.html",
  styleUrl: "./support-resistance-condition-item.component.scss"
})
export class SupportResistanceConditionItemComponent {
  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public readonly duplicate = new EventEmitter<void>();
  @Output() public readonly remove = new EventEmitter<void>();

  public readonly operators: SupportResistanceOperatorOption[] = SUPPORT_RESISTANCE_OPERATORS;

  public get needsTolerance(): boolean {
    const operator = String(this.group.get("operator")?.value ?? "");
    return operator === "near_support" || operator === "near_resistance" || operator === "bounce_support" || operator === "bounce_resistance";
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public onDuplicate(): void {
    this.duplicate.emit();
  }

  public onRemove(): void {
    this.remove.emit();
  }
}
