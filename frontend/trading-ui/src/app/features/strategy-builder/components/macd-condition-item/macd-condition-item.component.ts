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
import { MACD_OPERATORS, MacdOperatorOption } from "../../enums/macd-operator.enum";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-macd-condition-item",
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
  templateUrl: "./macd-condition-item.component.html",
  styleUrl: "./macd-condition-item.component.scss"
})
export class MacdConditionItemComponent {
  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public readonly duplicate = new EventEmitter<void>();
  @Output() public readonly remove = new EventEmitter<void>();

  public readonly operators: MacdOperatorOption[] = MACD_OPERATORS;

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public hasFastPeriodOrderingError(): boolean {
    const fastPeriodControl = this.group.get("fastPeriod");
    const slowPeriodControl = this.group.get("slowPeriod");
    if (fastPeriodControl === null || slowPeriodControl === null) {
      return false;
    }

    if (
      fastPeriodControl.hasError("required") ||
      fastPeriodControl.hasError("min") ||
      fastPeriodControl.hasError("max") ||
      slowPeriodControl.hasError("required") ||
      slowPeriodControl.hasError("min") ||
      slowPeriodControl.hasError("max")
    ) {
      return false;
    }

    const fastPeriod = Number(fastPeriodControl.value ?? 0);
    const slowPeriod = Number(slowPeriodControl.value ?? 0);
    const hasInteracted = fastPeriodControl.touched || fastPeriodControl.dirty || slowPeriodControl.touched || slowPeriodControl.dirty;

    return hasInteracted && fastPeriod >= slowPeriod;
  }

  public onDuplicate(): void {
    this.duplicate.emit();
  }

  public onRemove(): void {
    this.remove.emit();
  }
}