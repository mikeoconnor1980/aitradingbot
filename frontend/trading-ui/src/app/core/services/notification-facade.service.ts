import { inject, Injectable } from "@angular/core";
import { AppNotification, NotificationSeverity, NotificationType, NotifyOptions } from "../models/app-notification.model";
import { NotificationService } from "./notification.service";
import { NotificationStoreService } from "./notification-store.service";

/**
 * Unified notification facade that routes to both the snackbar (toast)
 * and the persistent notification panel based on severity defaults.
 *
 * Default routing:
 *  - success → toast only (ephemeral user-action feedback)
 *  - error   → toast + persist (reviewable in notification panel)
 *  - warning → toast + persist
 *  - info    → toast only
 *
 * Callers can override with explicit `toast` / `persist` flags.
 */
@Injectable({ providedIn: "root" })
export class NotificationFacade {
  private readonly _toast = inject(NotificationService);
  private readonly _store = inject(NotificationStoreService);

  public success(message: string, duration?: number): void {
    this.notify({ message, severity: "success", duration });
  }

  public error(message: string, duration?: number): void {
    this.notify({ message, severity: "error", duration });
  }

  public warning(message: string, duration?: number): void {
    this.notify({ message, severity: "warning", duration });
  }

  public info(message: string, duration?: number): void {
    this.notify({ message, severity: "info", duration });
  }

  public notify(options: NotifyOptions): void {
    const shouldToast = options.toast ?? true;
    const shouldPersist = options.persist ?? this._defaultPersist(options.severity);

    if (shouldToast) {
      this._showToast(options.message, options.severity, options.duration);
    }

    if (shouldPersist) {
      this._persistToStore(options);
    }
  }

  private _defaultPersist(severity: NotificationSeverity): boolean {
    return severity === "error" || severity === "warning";
  }

  private _showToast(message: string, severity: NotificationSeverity, duration?: number): void {
    switch (severity) {
      case "success":
        this._toast.success(message, duration);
        break;
      case "error":
        this._toast.error(message, duration);
        break;
      case "warning":
        this._toast.warning(message, duration);
        break;
      case "info":
        this._toast.info(message, duration);
        break;
    }
  }

  private _persistToStore(options: NotifyOptions): void {
    const notification: AppNotification = {
      id: crypto.randomUUID(),
      type: options.type ?? this._inferType(options.severity),
      title: options.title ?? this._inferTitle(options.severity),
      message: options.message,
      severity: options.severity,
      timestamp: new Date().toISOString(),
      read: false,
      data: options.data,
    };
    this._store.addExternal(notification);
  }

  private _inferType(severity: NotificationSeverity): NotificationType {
    return severity === "error" || severity === "warning" ? "Error" : "Action";
  }

  private _inferTitle(severity: NotificationSeverity): string {
    switch (severity) {
      case "error": return "Error";
      case "warning": return "Warning";
      case "success": return "Success";
      case "info": return "Info";
    }
  }
}
