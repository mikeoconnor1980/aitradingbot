import { Component, inject } from "@angular/core";
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { ModifyOrderDto } from "../../../../core/models/modify-order.model";
import { OpenOrder } from "../../../../core/models/open-order.model";

interface ModifyOrderForm {
  price: FormControl<number>;
  size: FormControl<number>;
}

export interface ModifyOrderDialogData {
  order: OpenOrder;
}

@Component({
  selector: "app-modify-order-modal",
  standalone: true,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: "./modify-order.modal.component.html",
  styleUrl: "./modify-order.modal.component.scss"
})
export class ModifyOrderModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<ModifyOrderModalComponent>);
  private readonly _data: ModifyOrderDialogData = inject(MAT_DIALOG_DATA);

  public readonly order = this._data.order;
  public readonly form: FormGroup<ModifyOrderForm> = this._fb.group<ModifyOrderForm>({
    price: this._fb.nonNullable.control(this._data.order.price, [Validators.required, Validators.min(0.000001)]),
    size: this._fb.nonNullable.control(this._data.order.size, [Validators.required, Validators.min(0.000001)])
  });

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: ModifyOrderDto = {
      price: this.form.controls.price.value,
      size: this.form.controls.size.value
    };

    this._dialogRef.close(result);
  }
}