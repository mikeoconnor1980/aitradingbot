import { computed, DestroyRef, inject, Injectable, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { bufferTime, filter } from "rxjs";
import { AppNotification, NotificationSeverity, NotificationType } from "../models/app-notification.model";
import { ConnectionStatus } from "../models/connection-status.model";
import { ExecutionLogEntry } from "../models/execution-log.model";
import { FillEvent } from "../models/fill-event.model";
import { OrderUpdate } from "../models/order-update.model";
import { SignalRService } from "./signalr.service";

@Injectable({ providedIn: "root" })
export class NotificationStoreService {
  private static readonly MAX_NOTIFICATIONS = 200;

  private readonly _signalR = inject(SignalRService);
  private readonly _destroyRef = inject(DestroyRef);

  private readonly _notifications = signal<AppNotification[]>([]);
  public readonly notifications = this._notifications.asReadonly();
  public readonly unreadCount = computed(() => this._notifications().filter(n => !n.read).length);

  public constructor() {
    this._subscribeToEvents();
  }

  public markAsRead(id: string): void {
    this._notifications.update(list =>
      list.map(n => n.id === id ? { ...n, read: true } : n)
    );
  }

  public markAllRead(): void {
    this._notifications.update(list =>
      list.map(n => n.read ? n : { ...n, read: true })
    );
  }

  public clear(): void {
    this._notifications.set([]);
  }

  private _subscribeToEvents(): void {
    // Buffer fills arriving within 500ms (Hyperliquid splits orders into partial fills)
    this._signalR.fillEvent$
      .pipe(
        bufferTime(500),
        filter((fills: FillEvent[]) => fills.length > 0),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((fills: FillEvent[]) => {
        const consolidated = this._consolidateFills(fills);
        for (const notification of consolidated) {
          this._addNotification(notification);
        }
      });

    this._signalR.orderUpdate$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((update: OrderUpdate) => this._addNotification(this._mapOrderUpdate(update)));

    this._signalR.connectionStatus$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((status: ConnectionStatus) => {
        // Skip the initial disconnected state before first connection
        if (status.source === "SignalR" && status.status === "Disconnected" && status.retryCount === 0) {
          return;
        }
        this._addNotification(this._mapConnection(status));
      });

    this._signalR.executionLog$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((entry: ExecutionLogEntry) => this._addNotification(this._mapExecutionLog(entry)));
  }

  private _addNotification(notification: AppNotification): void {
    this._notifications.update(list =>
      [notification, ...list].slice(0, NotificationStoreService.MAX_NOTIFICATIONS)
    );
  }

  /** Consolidate partial fills (same asset+side) into one notification per group. */
  private _consolidateFills(fills: FillEvent[]): AppNotification[] {
    const groups = new Map<string, FillEvent[]>();
    for (const fill of fills) {
      const key = `${fill.asset}|${fill.side}`;
      const group = groups.get(key) ?? [];
      group.push(fill);
      groups.set(key, group);
    }

    return Array.from(groups.values()).map(group => {
      const totalSize = group.reduce((sum, f) => sum + f.size, 0);
      const totalPnl = group.reduce((sum, f) => sum + f.closedPnl, 0);
      const vwap = group.reduce((sum, f) => sum + f.size * f.price, 0) / totalSize;
      const first = group[0]!;
      const partialNote = group.length > 1 ? ` (${group.length} fills)` : "";
      const pnl = totalPnl !== 0 ? ` | PnL: ${totalPnl >= 0 ? "+" : ""}${totalPnl.toFixed(2)}` : "";

      return {
        id: crypto.randomUUID(),
        type: "Fill" as NotificationType,
        title: `${first.side} ${+totalSize.toFixed(6)} ${first.asset}${partialNote}`,
        message: `@ ${vwap.toLocaleString(undefined, { maximumFractionDigits: 2 })}${pnl}`,
        severity: (totalPnl >= 0 ? "success" : "warning") as NotificationSeverity,
        timestamp: first.timestamp,
        read: false,
        data: group.length === 1 ? first : group,
      };
    });
  }

  private _mapOrderUpdate(update: OrderUpdate): AppNotification {
    const statusMap: Record<string, NotificationSeverity> = {
      filled: "success",
      triggered: "info",
      canceled: "warning",
      cancelled: "warning",
      rejected: "error",
    };
    const iconMap: Record<string, string> = {
      filled: "Filled",
      triggered: "Triggered",
      canceled: "Cancelled",
      cancelled: "Cancelled",
      rejected: "Rejected",
    };
    const label = iconMap[update.status.toLowerCase()] ?? update.status;
    return {
      id: crypto.randomUUID(),
      type: "OrderUpdate",
      title: `${label} — ${update.asset}`,
      message: `Order ${update.orderId}`,
      severity: statusMap[update.status.toLowerCase()] ?? "info",
      timestamp: update.timestamp,
      read: false,
      data: update,
    };
  }

  private _mapConnection(status: ConnectionStatus): AppNotification {
    const severityMap: Record<string, NotificationSeverity> = {
      Connected: "success",
      Reconnecting: "warning",
      Disconnected: "error"
    };
    return {
      id: crypto.randomUUID(),
      type: "Connection",
      title: `${status.source}: ${status.status}`,
      message: status.detail ?? "",
      severity: severityMap[status.status] ?? "info",
      timestamp: new Date().toISOString(),
      read: false,
      data: status
    };
  }

  private _mapExecutionLog(entry: ExecutionLogEntry): AppNotification {
    const severityMap: Record<string, NotificationSeverity> = {
      Critical: "error",
      Error: "error",
      Warning: "warning",
      Information: "info",
      Debug: "info",
      Trace: "info"
    };
    let type: NotificationType = "System";
    if (entry.category.toLowerCase().includes("order") || entry.category.toLowerCase().includes("fill")) {
      type = "OrderUpdate";
    }
    return {
      id: crypto.randomUUID(),
      type,
      title: entry.category,
      message: entry.message,
      severity: severityMap[entry.level] ?? "info",
      timestamp: entry.timestampUtc,
      read: false,
      data: entry
    };
  }
}
