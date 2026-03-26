import { Injectable, inject } from "@angular/core";
import { MatSnackBar } from "@angular/material/snack-bar";

export type NotificationSeverity = "success" | "error" | "warning" | "info";

@Injectable({ providedIn: "root" })
export class NotificationService {
  private readonly _snackBar = inject(MatSnackBar);

  public success(message: string, duration = 3000): void {
    this._show(message, "success", duration);
  }

  public error(message: string, duration = 5000): void {
    this._show(message, "error", duration);
  }

  public warning(message: string, duration = 4000): void {
    this._show(message, "warning", duration);
  }

  public info(message: string, duration = 3000): void {
    this._show(message, "info", duration);
  }

  private _show(message: string, severity: NotificationSeverity, duration: number): void {
    this._snackBar.open(message, "Dismiss", {
      duration,
      panelClass: [`snackbar--${severity}`],
      horizontalPosition: "right",
      verticalPosition: "top",
    });
  }
}
