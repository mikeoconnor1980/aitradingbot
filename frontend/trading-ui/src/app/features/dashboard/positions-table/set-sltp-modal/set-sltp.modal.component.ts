import { CommonModule } from "@angular/common";
import { Component, inject } from "@angular/core";
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { Position } from "../../../../core/models/position.model";

export interface SetSlTpDialogData {
  position: Position;
}

export interface SetSlTpResult {
  stopLossPrice: number | null;
  takeProfitPrice: number | null;
}

interface SetSlTpForm {
  stopLossPrice: FormControl<number | null>;
  takeProfitPrice: FormControl<number | null>;
}

@Component({
  selector: "app-set-sltp-modal",
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: "./set-sltp.modal.component.html",
  styleUrl: "./set-sltp.modal.component.scss"
})
export class SetSlTpModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<SetSlTpModalComponent>);

  public readonly data: SetSlTpDialogData = inject(MAT_DIALOG_DATA);
  public readonly isLong: boolean;
  public readonly form: FormGroup<SetSlTpForm>;

  public constructor() {
    this.isLong = this._isLongPosition(this.data.position);
    this.form = this._fb.group<SetSlTpForm>({
      stopLossPrice: this._fb.control<number | null>(this.data.position.stopLossPrice ?? null, {
        validators: [Validators.min(0.000001), this._createSlValidator()]
      }),
      takeProfitPrice: this._fb.control<number | null>(this.data.position.takeProfitPrice ?? null, {
        validators: [Validators.min(0.000001), this._createTpValidator()]
      })
    });
  }

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: SetSlTpResult = {
      stopLossPrice: this.form.controls.stopLossPrice.value,
      takeProfitPrice: this.form.controls.takeProfitPrice.value
    };

    this._dialogRef.close(result);
  }

  public isSlBeyondLiquidation(): boolean {
    const stopLossPrice = this.form.controls.stopLossPrice.value;
    const liquidationPrice = this.data.position.liquidationPrice;

    if (stopLossPrice == null || liquidationPrice <= 0) {
      return false;
    }

    if (this.isLong) {
      return stopLossPrice <= liquidationPrice;
    }

    return stopLossPrice >= liquidationPrice;
  }

  public getStopLossErrorMessage(): string | null {
    const control = this.form.controls.stopLossPrice;
    if (control.hasError("min")) {
      return "Stop loss must be greater than 0";
    }

    return control.getError("slInvalidSide") ?? null;
  }

  public getTakeProfitErrorMessage(): string | null {
    const control = this.form.controls.takeProfitPrice;
    if (control.hasError("min")) {
      return "Take profit must be greater than 0";
    }

    return control.getError("tpInvalidSide") ?? null;
  }

  private _isLongPosition(position: Position): boolean {
    return position.side === "Long" || position.size > 0;
  }

  private _createSlValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const stopLossPrice = control.value as number | null;
      if (stopLossPrice == null) {
        return null;
      }

      const entryPrice = this.data.position.entryPrice;
      if (this.isLong && stopLossPrice >= entryPrice) {
        return { slInvalidSide: "Stop loss must be below entry price for long positions" };
      }

      if (!this.isLong && stopLossPrice <= entryPrice) {
        return { slInvalidSide: "Stop loss must be above entry price for short positions" };
      }

      return null;
    };
  }

  private _createTpValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const takeProfitPrice = control.value as number | null;
      if (takeProfitPrice == null) {
        return null;
      }

      const entryPrice = this.data.position.entryPrice;
      if (this.isLong && takeProfitPrice <= entryPrice) {
        return { tpInvalidSide: "Take profit must be above entry price for long positions" };
      }

      if (!this.isLong && takeProfitPrice >= entryPrice) {
        return { tpInvalidSide: "Take profit must be below entry price for short positions" };
      }

      return null;
    };
  }
}