import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { catchError, map, of } from "rxjs";
import { AuthService } from "../services/auth.service";

export const adminRoleGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.currentUser?.isAdmin === true) {
    return true;
  }

  if (authService.isAuthenticated) {
    return authService.syncCurrentUser().pipe(
      map((user) => user.isAdmin ? true : router.createUrlTree(["/dashboard"])),
      catchError(() => of(router.createUrlTree(["/dashboard"])))
    );
  }

  return router.createUrlTree(["/dashboard"]);
};