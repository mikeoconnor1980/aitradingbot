import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { MatSnackBar } from "@angular/material/snack-bar";
import { LayoutService } from "../services/layout.service";

export const mobileRedirectGuard: CanActivateFn = () => {
  const layout = inject(LayoutService);
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);

  if (layout.isMobile()) {
    snackBar.open("This page is available on desktop", "OK", {
      duration: 3000,
      horizontalPosition: "center",
      verticalPosition: "top"
    });
    return router.createUrlTree(["/dashboard"]);
  }

  return true;
};
