import { DecimalPipe, TitleCasePipe, UpperCasePipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";

export interface ConfirmDialogData {
  side?: "buy" | "sell";
  orderType?: "market" | "limit";
  asset?: string;
  price?: number | null;
  size?: number;
  title?: string;
  message?: string;
  confirmText?: string;
  cancelText?: string;
}

@Component({
  selector: "app-confirm-dialog",
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, DecimalPipe, UpperCasePipe, TitleCasePipe],
  templateUrl: "./confirm-dialog.component.html",
  styleUrl: "./confirm-dialog.component.scss"
})
export class ConfirmDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<ConfirmDialogComponent>);

  public readonly data: ConfirmDialogData = inject(MAT_DIALOG_DATA);

  public hasOrderSummary(): boolean {
    return this.data.side !== undefined &&
      this.data.orderType !== undefined &&
      this.data.asset !== undefined &&
      this.data.size !== undefined;
  }

  public onConfirm(): void {
    this._dialogRef.close(true);
  }

  public onCancel(): void {
    this._dialogRef.close(false);
  }

  public getSideClass(): string {
    if (this.data.side === undefined) {
      return "";
    }

    return this.data.side === "buy" ? "confirm-dialog__side--buy" : "confirm-dialog__side--sell";
  }
}