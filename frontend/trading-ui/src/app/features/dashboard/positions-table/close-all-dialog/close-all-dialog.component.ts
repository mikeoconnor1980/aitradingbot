import { DecimalPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { Position } from "../../../../core/models/position.model";

export interface CloseAllDialogData {
  readonly positions: Position[];
}

export interface CloseAllResult {
  readonly confirmed: boolean;
  readonly succeeded: number;
  readonly failed: number;
  readonly total: number;
}

@Component({
  selector: "app-close-all-dialog",
  standalone: true,
  imports: [DecimalPipe, MatDialogModule, MatButtonModule],
  templateUrl: "./close-all-dialog.component.html",
  styleUrl: "./close-all-dialog.component.scss"
})
export class CloseAllDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<CloseAllDialogComponent>);
  private readonly _data: CloseAllDialogData = inject(MAT_DIALOG_DATA);

  public readonly positions: Position[] = this._data.positions;

  public get total(): number {
    return this.positions.length;
  }

  public onCancel(): void {
    this._dialogRef.close({
      confirmed: false,
      succeeded: 0,
      failed: 0,
      total: this.total
    } as CloseAllResult);
  }

  public onConfirm(): void {
    this._dialogRef.close({
      confirmed: true,
      succeeded: 0,
      failed: 0,
      total: this.total
    } as CloseAllResult);
  }
}
