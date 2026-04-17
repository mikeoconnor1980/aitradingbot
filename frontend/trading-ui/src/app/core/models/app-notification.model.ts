export type NotificationType = "Fill" | "OrderUpdate" | "System" | "Connection";
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
