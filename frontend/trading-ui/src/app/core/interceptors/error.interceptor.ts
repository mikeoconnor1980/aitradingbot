import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, throwError } from "rxjs";
import { NotificationService } from "../services/notification.service";
import { extractErrorCode, formatErrorPayload } from "../utils/error-utils";
import { SKIP_ERROR_NOTIFICATION } from "./http-context-tokens";

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  if (req.context.get(SKIP_ERROR_NOTIFICATION)) {
    return next(req);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const message = formatErrorPayload(error);
      const errorCode = extractErrorCode(error);

      if (errorCode === "rate_limit") {
        notifications.warning("Rate limited — please try again later");
      } else if (errorCode === "signing_error") {
        notifications.error("Signature rejected — check signing configuration");
      } else if (error.status === 0) {
        notifications.error("Cannot reach server — check your connection");
      } else if (error.status >= 500) {
        notifications.error(message);
      } else if (error.status >= 400) {
        notifications.warning(message);
      }

      return throwError(() => error);
    }),
  );
};
