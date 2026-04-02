import { inject } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { CanDeactivateFn } from "@angular/router";
import { map, Observable, of } from "rxjs";
import { ConfirmDialogComponent, ConfirmDialogData } from "../../order-entry/confirm-dialog/confirm-dialog.component";

export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component): Observable<boolean> => {
  if (!component.hasUnsavedChanges()) {
    return of(true);
  }

  const dialog = inject(MatDialog);
  const dialogData: ConfirmDialogData = {
    title: "Unsaved Changes",
    message: "You have unsaved changes. Are you sure you want to leave?",
    confirmText: "Leave",
    cancelText: "Stay"
  };

  return dialog.open(ConfirmDialogComponent, { data: dialogData, width: "400px" }).afterClosed().pipe(
    map((confirmed: boolean | undefined) => confirmed ?? false)
  );
};