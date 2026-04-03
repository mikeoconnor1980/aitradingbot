import { Component, Input, inject } from "@angular/core";
import { FormArray, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { ConditionFactoryService } from "../../services/condition-factory.service";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";
import { RsiConditionItemComponent } from "../rsi-condition-item/rsi-condition-item.component";

@Component({
  selector: "app-entry-conditions-card",
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatIconModule, InfoPopoverComponent, RsiConditionItemComponent],
  templateUrl: "./entry-conditions-card.component.html",
  styleUrl: "./entry-conditions-card.component.scss"
})
export class EntryConditionsCardComponent {
  private readonly _conditionFactory = inject(ConditionFactoryService);

  @Input() public conditions: FormArray | null = null;

  public get conditionGroups(): FormGroup[] {
    return (this.conditions?.controls as FormGroup[]) ?? [];
  }

  public get isBound(): boolean {
    return this.conditions !== null;
  }

  public onAddRsi(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createRsiCondition());
  }

  public onDuplicate(index: number): void {
    if (this.conditions === null) {
      return;
    }

    const source = this.conditions.at(index) as FormGroup;
    const values = source.getRawValue() as Record<string, unknown>;

    this.conditions.insert(index + 1, this._conditionFactory.createRsiCondition({
      enabled: values["enabled"] as boolean,
      label: values["label"] as string,
      period: values["period"] as number,
      operator: values["operator"] as "lt" | "lte" | "gt" | "gte" | "cross_above" | "cross_below",
      value: values["value"] as number,
    }));
  }

  public onRemove(index: number): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.removeAt(index);
  }
}