import { Injectable, inject } from "@angular/core";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator } from "../models/strategy.model";

export interface CreateRsiConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  period: number;
  operator: RsiOperator;
  value: number;
}

export interface CreatePriceVsEmaConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  period: number;
  operator: PriceVsEmaOperator;
  distanceType: PriceVsEmaDistanceType;
  distanceValue: number | null;
}

@Injectable({ providedIn: "root" })
export class ConditionFactoryService {
  private readonly _fb = inject(FormBuilder);

  private _nextId = 1;

  public createRsiCondition(overrides?: Partial<CreateRsiConditionOverrides>): FormGroup {
    if (overrides?.id !== undefined) {
      this._advancePastId(overrides.id);
    }

    return this._fb.group({
      id: [overrides?.id ?? this._generateId()],
      enabled: [overrides?.enabled ?? true],
      type: ["rsi"],
      label: [overrides?.label ?? ""],
      period: [overrides?.period ?? 14, [Validators.required, Validators.min(1)]],
      operator: [overrides?.operator ?? "lt", Validators.required],
      value: [overrides?.value ?? 40, [Validators.required, Validators.min(0), Validators.max(100)]],
    });
  }

  public createPriceVsEmaCondition(overrides?: Partial<CreatePriceVsEmaConditionOverrides>): FormGroup {
    if (overrides?.id !== undefined) {
      this._advancePastId(overrides.id);
    }

    const group = this._fb.group({
      id: [overrides?.id ?? this._generateId()],
      enabled: [overrides?.enabled ?? true],
      type: ["price_vs_ema"],
      label: [overrides?.label ?? ""],
      period: [overrides?.period ?? 50, [Validators.required, Validators.min(1)]],
      operator: [overrides?.operator ?? "near", Validators.required],
      distanceType: [overrides?.distanceType ?? "percent", Validators.required],
      distanceValue: [overrides?.distanceValue ?? 0.25, [Validators.required, Validators.min(0.01)]],
    });

    if (group.get("operator")?.value !== "near") {
      group.get("distanceType")?.disable({ emitEvent: false });
      group.get("distanceValue")?.disable({ emitEvent: false });
    }

    return group;
  }

  private _generateId(): string {
    return `cond-${this._nextId++}`;
  }

  private _advancePastId(id: string): void {
    const match = /^cond-(\d+)$/.exec(id);
    if (match !== null) {
      const existingNumber = Number(match[1]);
      if (existingNumber >= this._nextId) {
        this._nextId = existingNumber + 1;
      }
    }
  }
}