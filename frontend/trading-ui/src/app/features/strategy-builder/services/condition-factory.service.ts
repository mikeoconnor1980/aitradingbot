import { Injectable, inject } from "@angular/core";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { MacdOperator, PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator, SupportResistanceOperator } from "../models/strategy.model";

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

export interface CreateMacdConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  fastPeriod: number;
  slowPeriod: number;
  signalPeriod: number;
  operator: MacdOperator;
}

export interface CreateSupportResistanceConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  lookback: number;
  strength: number;
  operator: SupportResistanceOperator;
  tolerance: number;
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

  public createMacdCondition(overrides?: Partial<CreateMacdConditionOverrides>): FormGroup {
    if (overrides?.id !== undefined) {
      this._advancePastId(overrides.id);
    }

    return this._fb.group({
      id: [overrides?.id ?? this._generateId()],
      enabled: [overrides?.enabled ?? true],
      type: ["macd"],
      label: [overrides?.label ?? ""],
      fastPeriod: [overrides?.fastPeriod ?? 12, [Validators.required, Validators.min(2), Validators.max(50)]],
      slowPeriod: [overrides?.slowPeriod ?? 26, [Validators.required, Validators.min(5), Validators.max(200)]],
      signalPeriod: [overrides?.signalPeriod ?? 9, [Validators.required, Validators.min(2), Validators.max(50)]],
      operator: [overrides?.operator ?? "cross_above_signal", Validators.required],
    });
  }

  public createSupportResistanceCondition(overrides?: Partial<CreateSupportResistanceConditionOverrides>): FormGroup {
    if (overrides?.id !== undefined) {
      this._advancePastId(overrides.id);
    }

    return this._fb.group({
      id: [overrides?.id ?? this._generateId()],
      enabled: [overrides?.enabled ?? true],
      type: ["support_resistance"],
      label: [overrides?.label ?? ""],
      lookback: [overrides?.lookback ?? 50, [Validators.required, Validators.min(10), Validators.max(500)]],
      strength: [overrides?.strength ?? 3, [Validators.required, Validators.min(1), Validators.max(10)]],
      operator: [overrides?.operator ?? "near_support", Validators.required],
      tolerance: [overrides?.tolerance ?? 0.5, [Validators.required, Validators.min(0), Validators.max(10)]],
    });
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