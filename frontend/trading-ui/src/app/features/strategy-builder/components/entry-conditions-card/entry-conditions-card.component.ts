import { Component, Input, inject } from "@angular/core";
import { FormArray, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MACD_OPERATORS, MacdOperatorOption } from "../../enums/macd-operator.enum";
import { ConditionFactoryService } from "../../services/condition-factory.service";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";
import { PriceVsEmaConditionItemComponent } from "../price-vs-ema-condition-item/price-vs-ema-condition-item.component";
import { RsiConditionItemComponent } from "../rsi-condition-item/rsi-condition-item.component";

@Component({
  selector: "app-entry-conditions-card",
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
    RsiConditionItemComponent,
    PriceVsEmaConditionItemComponent,
  ],
  templateUrl: "./entry-conditions-card.component.html",
  styleUrl: "./entry-conditions-card.component.scss"
})
export class EntryConditionsCardComponent {
  private readonly _conditionFactory = inject(ConditionFactoryService);

  @Input() public conditions: FormArray | null = null;

  public readonly macdOperators: MacdOperatorOption[] = MACD_OPERATORS;

  public get conditionGroups(): FormGroup[] {
    return (this.conditions?.controls as FormGroup[]) ?? [];
  }

  public get isBound(): boolean {
    return this.conditions !== null;
  }

  public getConditionType(group: FormGroup): string {
    return String(group.get("type")?.value ?? "rsi");
  }

  public hasError(group: FormGroup, controlName: string, errorCode: string): boolean {
    const control = group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public onAddRsi(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createRsiCondition());
  }

  public onAddPriceVsEma(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createPriceVsEmaCondition());
  }

  public onAddMacd(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createMacdCondition());
  }

  public onDuplicate(index: number): void {
    if (this.conditions === null) {
      return;
    }

    const source = this.conditions.at(index) as FormGroup;
    const values = source.getRawValue() as Record<string, unknown>;

    if (String(values["type"] ?? "rsi") === "price_vs_ema") {
      this.conditions.insert(index + 1, this._conditionFactory.createPriceVsEmaCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        period: values["period"] as number,
        operator: values["operator"] as "near" | "above" | "below" | "cross_above" | "cross_below" | "touch",
        distanceType: values["distanceType"] as "percent" | "atr_multiple" | "absolute",
        distanceValue: values["distanceValue"] as number | null,
      }));
      return;
    }

    if (String(values["type"] ?? "rsi") === "macd") {
      this.conditions.insert(index + 1, this._conditionFactory.createMacdCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        fastPeriod: values["fastPeriod"] as number,
        slowPeriod: values["slowPeriod"] as number,
        signalPeriod: values["signalPeriod"] as number,
        operator: values["operator"] as "cross_above" | "cross_below" | "gt" | "lt",
      }));
      return;
    }

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