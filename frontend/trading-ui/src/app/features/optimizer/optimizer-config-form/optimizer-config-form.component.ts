import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { RunOptimizationRequest } from "../../../core/models/optimizer.model";

interface OptimizerConfigFormModel {
  symbol: FormControl<string>;
  startDate: FormControl<Date | null>;
  endDate: FormControl<Date | null>;
  initialCapital: FormControl<number>;
  sampleSize: FormControl<number>;
  stopLossMin: FormControl<number>;
  stopLossMax: FormControl<number>;
  takeProfitMin: FormControl<number>;
  takeProfitMax: FormControl<number>;
  leverageMin: FormControl<number>;
  leverageMax: FormControl<number>;
  minWinRate: FormControl<number>;
  minTotalTrades: FormControl<number>;
  maxDrawdownPercent: FormControl<number>;
}

function normalizeDateOnly(date: Date): Date {
  const normalizedDate = new Date(date);
  normalizedDate.setHours(0, 0, 0, 0);
  return normalizedDate;
}

function futureDateValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;

  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }

  return normalizeDateOnly(value) > normalizeDateOnly(new Date())
    ? { futureDate: true }
    : null;
}

function dateRangeValidator(control: AbstractControl): ValidationErrors | null {
  const formGroup = control as FormGroup<OptimizerConfigFormModel>;
  const startDate = formGroup.controls.startDate.value;
  const endDate = formGroup.controls.endDate.value;

  if (startDate === null || endDate === null) {
    return null;
  }

  return startDate < endDate ? null : { dateRange: true };
}

function minMaxValidator(minControlName: keyof OptimizerConfigFormModel, maxControlName: keyof OptimizerConfigFormModel, errorKey: string) {
  return (control: AbstractControl): ValidationErrors | null => {
    const formGroup = control as FormGroup<OptimizerConfigFormModel>;
    const minValue = Number(formGroup.controls[minControlName].value);
    const maxValue = Number(formGroup.controls[maxControlName].value);

    if (!Number.isFinite(minValue) || !Number.isFinite(maxValue)) {
      return null;
    }

    return minValue <= maxValue ? null : { [errorKey]: true };
  };
}

@Component({
  selector: "app-optimizer-config-form",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./optimizer-config-form.component.html",
  styleUrl: "./optimizer-config-form.component.scss"
})
export class OptimizerConfigFormComponent {
  @Input()
  public isRunning = false;

  @Output()
  public runOptimization = new EventEmitter<RunOptimizationRequest>();

  public submitted = false;
  public readonly maxSelectableDate = normalizeDateOnly(new Date());
  public readonly form = new FormGroup<OptimizerConfigFormModel>({
    symbol: new FormControl<string>("BTCUSDT", { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl<Date | null>(new Date(new Date().setMonth(new Date().getMonth() - 3)), { validators: [Validators.required, futureDateValidator] }),
    endDate: new FormControl<Date | null>(new Date(), { validators: [Validators.required, futureDateValidator] }),
    initialCapital: new FormControl<number>(10000, { nonNullable: true, validators: [Validators.required, Validators.min(100)] }),
    sampleSize: new FormControl<number>(500, { nonNullable: true, validators: [Validators.required, Validators.min(10), Validators.max(5000)] }),
    stopLossMin: new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.1)] }),
    stopLossMax: new FormControl<number>(5, { nonNullable: true, validators: [Validators.required, Validators.min(0.1)] }),
    takeProfitMin: new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.1)] }),
    takeProfitMax: new FormControl<number>(6, { nonNullable: true, validators: [Validators.required, Validators.min(0.1)] }),
    leverageMin: new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    leverageMax: new FormControl<number>(5, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    minWinRate: new FormControl<number>(40, { nonNullable: true, validators: [Validators.required, Validators.min(0), Validators.max(100)] }),
    minTotalTrades: new FormControl<number>(10, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    maxDrawdownPercent: new FormControl<number>(30, { nonNullable: true, validators: [Validators.required, Validators.min(0.1), Validators.max(100)] })
  }, {
    validators: [
      dateRangeValidator,
      minMaxValidator("stopLossMin", "stopLossMax", "stopLossRange"),
      minMaxValidator("takeProfitMin", "takeProfitMax", "takeProfitRange"),
      minMaxValidator("leverageMin", "leverageMax", "leverageRange")
    ]
  });

  public onSubmit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    const startDate = this.form.controls.startDate.value;
    const endDate = this.form.controls.endDate.value;

    if (startDate === null || endDate === null) {
      return;
    }

    this.runOptimization.emit({
      symbol: this.form.controls.symbol.value.trim().toUpperCase(),
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
      initialCapital: this.form.controls.initialCapital.value,
      sampleSize: this.form.controls.sampleSize.value,
      stopLossMin: this.form.controls.stopLossMin.value,
      stopLossMax: this.form.controls.stopLossMax.value,
      takeProfitMin: this.form.controls.takeProfitMin.value,
      takeProfitMax: this.form.controls.takeProfitMax.value,
      leverageMin: this.form.controls.leverageMin.value,
      leverageMax: this.form.controls.leverageMax.value,
      minWinRate: this.form.controls.minWinRate.value,
      minTotalTrades: this.form.controls.minTotalTrades.value,
      maxDrawdownPercent: this.form.controls.maxDrawdownPercent.value,
    });
  }

  public hasControlError(name: keyof OptimizerConfigFormModel): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted);
  }

  public getControlErrorMessage(name: keyof OptimizerConfigFormModel): string {
    const control = this.form.controls[name];

    if (control.hasError("required")) {
      return "This field is required.";
    }

    if (control.hasError("futureDate")) {
      return "Date cannot be in the future.";
    }

    if (control.hasError("min")) {
      return "Value is below the allowed minimum.";
    }

    if (control.hasError("max")) {
      return "Value exceeds the allowed maximum.";
    }

    return "Invalid value.";
  }

  public get formErrorMessage(): string | null {
    if (this.form.hasError("dateRange")) {
      return "End date must be after the start date.";
    }

    if (this.form.hasError("stopLossRange")) {
      return "Stop loss max must be greater than or equal to min.";
    }

    if (this.form.hasError("takeProfitRange")) {
      return "Take profit max must be greater than or equal to min.";
    }

    if (this.form.hasError("leverageRange")) {
      return "Leverage max must be greater than or equal to min.";
    }

    return null;
  }
}