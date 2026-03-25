import { DecimalPipe, TitleCasePipe, UpperCasePipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";

export interface ConfirmDialogData {
  side: "buy" | "sell";
  orderType: "market" | "limit";
  asset: string;
  price: number | null;
  size: number;
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

  public onConfirm(): void {
    this._dialogRef.close(true);
  }

  public onCancel(): void {
    this._dialogRef.close(false);
  }

  public getSideClass(): string {
    return this.data.side === "buy" ? "confirm-dialog__side--buy" : "confirm-dialog__side--sell";
  }
}