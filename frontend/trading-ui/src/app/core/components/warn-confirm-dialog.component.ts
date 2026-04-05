import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";

export interface WarnConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  warnIcon?: string;
}

@Component({
  selector: "app-warn-confirm-dialog",
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="warn-dialog__title">
      <mat-icon class="warn-dialog__icon">{{ data.warnIcon ?? "warning" }}</mat-icon>
      {{ data.title }}
    </h2>

    <mat-dialog-content>
      <p class="warn-dialog__message">{{ data.message }}</p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="onCancel()">{{ data.cancelText ?? "Cancel" }}</button>
      <button mat-flat-button color="warn" type="button" (click)="onConfirm()">{{ data.confirmText ?? "Continue" }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .warn-dialog__title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .warn-dialog__icon {
      color: #f59e0b;
    }

    .warn-dialog__message {
      line-height: 1.5;
    }
  `]
})
export class WarnConfirmDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<WarnConfirmDialogComponent>);

  public readonly data: WarnConfirmDialogData = inject(MAT_DIALOG_DATA);

  public onConfirm(): void {
    this._dialogRef.close(true);
  }

  public onCancel(): void {
    this._dialogRef.close(false);
  }
}
