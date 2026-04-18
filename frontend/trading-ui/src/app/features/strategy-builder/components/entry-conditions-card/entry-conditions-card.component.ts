import { Component, Input, inject } from "@angular/core";
import { AbstractControl, FormArray, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ConditionFactoryService } from "../../services/condition-factory.service";
import { CandlePatternConditionItemComponent } from "../candle-pattern-condition-item/candle-pattern-condition-item.component";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";
import { LiquiditySweepConditionItemComponent } from "../liquidity-sweep-condition-item/liquidity-sweep-condition-item.component";
import { MacdConditionItemComponent } from "../macd-condition-item/macd-condition-item.component";
import { PriceVsEmaConditionItemComponent } from "../price-vs-ema-condition-item/price-vs-ema-condition-item.component";
import { RsiConditionItemComponent } from "../rsi-condition-item/rsi-condition-item.component";
import { StructureShiftConditionItemComponent } from "../structure-shift-condition-item/structure-shift-condition-item.component";
import { SupportResistanceConditionItemComponent } from "../support-resistance-condition-item/support-resistance-condition-item.component";

@Component({
  selector: "app-entry-conditions-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    CandlePatternConditionItemComponent,
    InfoPopoverComponent,
    LiquiditySweepConditionItemComponent,
    RsiConditionItemComponent,
    PriceVsEmaConditionItemComponent,
    MacdConditionItemComponent,
    StructureShiftConditionItemComponent,
    SupportResistanceConditionItemComponent,
  ],
  templateUrl: "./entry-conditions-card.component.html",
  styleUrl: "./entry-conditions-card.component.scss"
})
export class EntryConditionsCardComponent {
  private readonly _conditionFactory = inject(ConditionFactoryService);

  @Input() public conditions: FormArray | null = null;
  @Input() public entryLogicControl: AbstractControl | null = null;

  public get conditionGroups(): FormGroup[] {
    return (this.conditions?.controls as FormGroup[]) ?? [];
  }

  public get isBound(): boolean {
    return this.conditions !== null;
  }

  public get hasMacdCondition(): boolean {
    return this.conditionGroups.some((group) => this.getConditionType(group) === "macd");
  }

  public getConditionType(group: FormGroup): string {
    return String(group.get("type")?.value ?? "rsi");
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
    if (this.conditions === null || this.hasMacdCondition) {
      return;
    }

    this.conditions.push(this._conditionFactory.createMacdCondition());
  }

  public onAddSupportResistance(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createSupportResistanceCondition());
  }

  public onAddCandlePattern(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createCandlePatternCondition());
  }

  public onAddLiquiditySweep(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createLiquiditySweepCondition());
  }

  public onAddStructureShift(): void {
    if (this.conditions === null) {
      return;
    }

    this.conditions.push(this._conditionFactory.createStructureShiftCondition());
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
      if (this.hasMacdCondition) {
        return;
      }

      this.conditions.insert(index + 1, this._conditionFactory.createMacdCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        fastPeriod: values["fastPeriod"] as number,
        slowPeriod: values["slowPeriod"] as number,
        signalPeriod: values["signalPeriod"] as number,
        operator: values["operator"] as "cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling",
      }));
      return;
    }

    if (String(values["type"] ?? "rsi") === "support_resistance") {
      this.conditions.insert(index + 1, this._conditionFactory.createSupportResistanceCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        lookback: values["lookback"] as number,
        strength: values["strength"] as number,
        operator: values["operator"] as "near_support" | "near_resistance" | "above_support" | "below_resistance" | "bounce_support" | "bounce_resistance",
        tolerance: values["tolerance"] as number,
      }));
      return;
    }

    if (String(values["type"] ?? "rsi") === "candle_pattern") {
      this.conditions.insert(index + 1, this._conditionFactory.createCandlePatternCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        pattern: values["pattern"] as "bullish_engulfing" | "bearish_engulfing" | "bullish_rejection" | "bearish_rejection" | "bullish_continuation" | "bearish_continuation" | "bullish_rejection_or_engulfing" | "bearish_rejection_or_engulfing",
      }));
      return;
    }

    if (String(values["type"] ?? "rsi") === "liquidity_sweep") {
      this.conditions.insert(index + 1, this._conditionFactory.createLiquiditySweepCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        lookbackBars: values["lookbackBars"] as number,
        pivotBars: values["pivotBars"] as number,
        side: values["side"] as "upside" | "downside",
      }));
      return;
    }

    if (String(values["type"] ?? "rsi") === "structure_shift") {
      this.conditions.insert(index + 1, this._conditionFactory.createStructureShiftCondition({
        enabled: values["enabled"] as boolean,
        label: values["label"] as string,
        pivotBars: values["pivotBars"] as number,
        direction: values["direction"] as "bullish" | "bearish",
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