import { Component, Input, inject } from "@angular/core";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";

@Component({
  selector: "app-dca-config-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: "./dca-config-card.component.html",
  styleUrl: "./dca-config-card.component.scss"
})
export class DcaConfigCardComponent {
  private readonly _fb = inject(FormBuilder);

  @Input({ required: true }) public group!: FormGroup;

  public readonly dayOptions = [
    { value: 0, label: "Sunday" },
    { value: 1, label: "Monday" },
    { value: 2, label: "Tuesday" },
    { value: 3, label: "Wednesday" },
    { value: 4, label: "Thursday" },
    { value: 5, label: "Friday" },
    { value: 6, label: "Saturday" },
  ];

  public get scalingBands(): FormArray {
    return this.group.get("scalingBands") as FormArray;
  }

  public get showsDayOfWeek(): boolean {
    const interval = String(this.group.get("interval")?.value ?? "weekly");
    return interval === "weekly" || interval === "biweekly";
  }

  public get showsDayOfMonth(): boolean {
    return this.group.get("interval")?.value === "monthly";
  }

  public get timeStepSeconds(): number {
    return this.group.get("interval")?.value === "five_minutes" ? 300 : 3600;
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public hasBandError(index: number, controlName: string, errorCode: string): boolean {
    const control = this.getScalingBand(index).get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public getScalingBand(index: number): FormGroup {
    return this.scalingBands.at(index) as FormGroup;
  }

  public addScalingBand(): void {
    if (this.scalingBands.length >= 5) {
      return;
    }

    this.scalingBands.push(this._createScalingBandGroup());
    this.scalingBands.markAsDirty();
  }

  public removeScalingBand(index: number): void {
    this.scalingBands.removeAt(index);
    this.scalingBands.markAsDirty();
  }

  private _createScalingBandGroup(): FormGroup {
    return this._fb.group({
      priceLowerUsd: [null, [Validators.min(0.00000001)]],
      priceUpperUsd: [null, [Validators.min(0.00000001)]],
      scalingPercent: [0, [Validators.required, Validators.min(-100), Validators.max(500)]],
    });
  }
}