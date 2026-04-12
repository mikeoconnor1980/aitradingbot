import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { SubscriptionService } from "../services/subscription.service";
import { map, catchError, of } from "rxjs";
import { HttpClient, HttpContext } from "@angular/common/http";
import { environment } from "../../../environments/environment";
import { SubscriptionStatusResponse } from "../services/subscription.service";
import { SKIP_ERROR_NOTIFICATION } from "../interceptors/http-context-tokens";

export const subscriptionGuard: CanActivateFn = () => {
  const subscriptionService = inject(SubscriptionService);
  const router = inject(Router);
  const http = inject(HttpClient);
  const denyTree = router.createUrlTree(["/dashboard"], { queryParams: { needsSubscription: "true" } });

  const cached = subscriptionService.currentStatus;
  if (cached !== null) {
    return cached.isActive ? true : denyTree;
  }

  return http.get<SubscriptionStatusResponse>(`${environment.apiBaseUrl}/subscriptions/status`, {
    context: new HttpContext().set(SKIP_ERROR_NOTIFICATION, true)
  }).pipe(
    map((status) => {
      subscriptionService.setStatus(status);
      return status.isActive ? true : denyTree;
    }),
    catchError(() => of(denyTree))
  );
};
