export type NotificationType = "Fill" | "OrderUpdate" | "System" | "Connection" | "Action" | "Error";
export type NotificationSeverity = "info" | "success" | "warning" | "error";

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  severity: NotificationSeverity;
  timestamp: string;
  read: boolean;
  data?: unknown;
}

export interface NotifyOptions {
  type?: NotificationType;
  title?: string;
  message: string;
  severity: NotificationSeverity;
  /** Show ephemeral snackbar toast. Defaults based on severity. */
  toast?: boolean;
  /** Persist to notification panel. Defaults based on severity. */
  persist?: boolean;
  /** Toast auto-dismiss duration in ms. */
  duration?: number;
  data?: unknown;
}
