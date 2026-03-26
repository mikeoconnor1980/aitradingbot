import { HttpErrorResponse } from "@angular/common/http";

/**
 * Extracts a human-readable error message from an HttpErrorResponse.
 * Handles the backend Envelope shape (errorMessage), ProblemDetails (detail/title),
 * and plain string error bodies.
 */
export function formatErrorPayload(errorResponse: HttpErrorResponse): string {
  if (typeof errorResponse.error === "string" && errorResponse.error.length > 0) {
    return errorResponse.error;
  }

  if (errorResponse.error !== null && errorResponse.error !== undefined) {
    if (typeof errorResponse.error === "object" && errorResponse.error["errorMessage"]) {
      return String(errorResponse.error["errorMessage"]);
    }
    if (typeof errorResponse.error === "object" && errorResponse.error["detail"]) {
      return String(errorResponse.error["detail"]);
    }
    if (typeof errorResponse.error === "object" && errorResponse.error["title"]) {
      return String(errorResponse.error["title"]);
    }
    return "An unexpected error occurred";
  }

  return errorResponse.message || "Unknown error";
}

/**
 * Extracts the error code from an HttpErrorResponse if it carries an Envelope body.
 */
export function extractErrorCode(errorResponse: HttpErrorResponse): string | null {
  if (
    errorResponse.error !== null &&
    typeof errorResponse.error === "object" &&
    errorResponse.error["errorCode"]
  ) {
    return String(errorResponse.error["errorCode"]);
  }
  return null;
}
