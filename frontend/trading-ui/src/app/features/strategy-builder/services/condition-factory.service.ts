import { Injectable, inject } from "@angular/core";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { RsiOperator } from "../models/strategy.model";

export interface CreateRsiConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  period: number;
  operator: RsiOperator;
  value: number;
}

@Injectable({ providedIn: "root" })
export class ConditionFactoryService {
  private readonly _fb = inject(FormBuilder);

  private _nextId = 1;

  public createRsiCondition(overrides?: Partial<CreateRsiConditionOverrides>): FormGroup {
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

  private _generateId(): string {
    return `cond-${this._nextId++}`;
  }
}