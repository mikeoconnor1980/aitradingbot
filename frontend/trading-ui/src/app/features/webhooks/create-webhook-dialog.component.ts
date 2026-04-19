import { CommonModule } from "@angular/common";
import { Component, Inject, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { AgentInfo } from "../../core/services/agent.service";
import { TradableAsset } from "../../core/models/tradable-asset.model";

export interface CreateWebhookDialogData {
  assets: TradableAsset[];
  agents: AgentInfo[];
}

export interface CreateWebhookDialogResult {
  label: string;
  defaultAsset: string | null;
  targetAgentId: string | null;
}

@Component({
  selector: "app-create-webhook-dialog",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: "./create-webhook-dialog.component.html",
  styleUrl: "./create-webhook-dialog.component.scss"
})
export class CreateWebhookDialogComponent {
  private readonly _formBuilder = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<CreateWebhookDialogComponent, CreateWebhookDialogResult>);

  public readonly form = this._formBuilder.group({
    label: ["", [Validators.required, Validators.maxLength(120)]],
    defaultAsset: [null as string | null],
    targetAgentId: [null as string | null]
  });

  public constructor(@Inject(MAT_DIALOG_DATA) public readonly data: CreateWebhookDialogData) {}

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onCreate(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this._dialogRef.close({
      label: this.form.controls.label.value!.trim(),
      defaultAsset: this.form.controls.defaultAsset.value,
      targetAgentId: this.form.controls.targetAgentId.value
    });
  }
}