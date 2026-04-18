import { Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";

export interface RenameStrategyTemplateDialogData {
  name: string;
  description: string;
  existingNames: string[];
}

export interface RenameStrategyTemplateDialogResult {
  name: string;
  description: string;
}

@Component({
  selector: "app-rename-strategy-template-dialog",
  standalone: true,
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: "./rename-strategy-template-dialog.component.html",
  styleUrl: "./rename-strategy-template-dialog.component.scss"
})
export class RenameStrategyTemplateDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<RenameStrategyTemplateDialogComponent>);
  private readonly _fb = inject(FormBuilder);

  public readonly data: RenameStrategyTemplateDialogData = inject(MAT_DIALOG_DATA);

  public readonly form = this._fb.group({
    name: [this.data.name, [Validators.required, Validators.maxLength(100), this._uniqueNameValidator(this.data.existingNames)]],
    description: [this.data.description, [Validators.required, Validators.maxLength(500)]],
  });

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this._dialogRef.close({
      name: this.form.controls.name.value?.trim() ?? "",
      description: this.form.controls.description.value?.trim() ?? ""
    } satisfies RenameStrategyTemplateDialogResult);
  }

  private _uniqueNameValidator(existingNames: string[]) {
    const normalizedExistingNames = new Set(existingNames.map((name) => name.trim().toLowerCase()));

    return (control: { value: string | null }) => {
      const value = control.value?.trim().toLowerCase() ?? "";
      return value.length > 0 && normalizedExistingNames.has(value)
        ? { duplicateName: true }
        : null;
    };
  }
}