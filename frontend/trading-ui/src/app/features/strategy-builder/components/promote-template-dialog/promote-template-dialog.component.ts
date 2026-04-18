import { Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { TagSelectorComponent } from "../tag-selector/tag-selector.component";

export interface PromoteTemplateDialogData {
  defaultName: string;
  existingNames: string[];
  availableTags: string[];
  initialTags: string[];
}

export interface PromoteTemplateDialogResult {
  name: string;
  description: string;
  tags: string[];
}

@Component({
  selector: "app-promote-template-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TagSelectorComponent,
  ],
  templateUrl: "./promote-template-dialog.component.html",
  styleUrl: "./promote-template-dialog.component.scss"
})
export class PromoteTemplateDialogComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<PromoteTemplateDialogComponent>);

  public readonly data: PromoteTemplateDialogData = inject(MAT_DIALOG_DATA);
  public readonly form = this._fb.group({
    name: [
      this.data.defaultName,
      [Validators.required, Validators.maxLength(100), this._duplicateNameValidator(this.data.existingNames)]
    ],
    description: ["", [Validators.required, Validators.maxLength(500)]],
    tags: [this._normalizeTags(this.data.initialTags)],
  });

  public get tagsControl() {
    return this.form.get("tags");
  }

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onPromote(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this._dialogRef.close({
      name: String(this.form.get("name")?.value ?? "").trim(),
      description: String(this.form.get("description")?.value ?? "").trim(),
      tags: this._normalizeTags(this.tagsControl?.value as string[] | null | undefined),
    } satisfies PromoteTemplateDialogResult);
  }

  private _duplicateNameValidator(existingNames: string[]): ValidatorFn {
    const normalizedNames = new Set(existingNames.map((name) => this._normalizeName(name)));

    return (control): ValidationErrors | null => {
      const candidateName = this._normalizeName(String(control.value ?? ""));

      if (candidateName.length === 0) {
        return null;
      }

      return normalizedNames.has(candidateName) ? { duplicateName: true } : null;
    };
  }

  private _normalizeName(value: string): string {
    return value.trim().toLocaleLowerCase();
  }

  private _normalizeTags(tags: string[] | null | undefined): string[] {
    return Array.isArray(tags)
      ? tags
        .map((tag) => String(tag).trim())
        .filter((tag) => tag.length > 0)
        .filter((tag, index, allTags) => allTags.indexOf(tag) === index)
      : [];
  }
}