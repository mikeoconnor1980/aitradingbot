import { ComponentType } from "@angular/cdk/portal";
import { Injectable, inject } from "@angular/core";
import { MatDialog, MatDialogConfig, MatDialogRef } from "@angular/material/dialog";
import { LayoutService } from "./layout.service";

@Injectable({ providedIn: "root" })
export class ResponsiveDialogService {
  private readonly _dialog = inject(MatDialog);
  private readonly _layout = inject(LayoutService);

  public open<T, D = unknown, R = any>(
    component: ComponentType<T>,
    config?: MatDialogConfig<D>
  ): MatDialogRef<T, R> {
    if (this._layout.isMobile()) {
      return this._dialog.open<T, D, R>(component, {
        ...config,
        width: "100vw",
        maxWidth: "100vw",
        position: { bottom: "0" },
        panelClass: "responsive-dialog--mobile"
      });
    }

    return this._dialog.open<T, D, R>(component, config);
  }
}
