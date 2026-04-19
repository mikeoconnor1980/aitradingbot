import { Component, DestroyRef, computed, EventEmitter, inject, Input, Output, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { interval } from "rxjs";
import { AppNotification, NotificationType } from "../../models/app-notification.model";
import { NotificationStoreService } from "../../services/notification-store.service";
import { RelativeTimePipe } from "./relative-time.pipe";

type FilterOption = "All" | "Fill" | "OrderUpdate" | "System" | "Connection";

@Component({
  selector: "app-notification-panel",
  standalone: true,
  imports: [MatIconModule, MatButtonModule, MatTooltipModule, RelativeTimePipe],
  templateUrl: "./notification-panel.component.html",
  styleUrl: "./notification-panel.component.scss"
})
export class NotificationPanelComponent {
  private readonly _store = inject(NotificationStoreService);
  private readonly _destroyRef = inject(DestroyRef);

  @Input()
  public isOpen = false;

  @Output()
  public closed = new EventEmitter<void>();

  public readonly activeFilter = signal<FilterOption>("All");
  public readonly relativeTimeRefresh = signal(Date.now());

  public constructor() {
    interval(10000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this.relativeTimeRefresh.set(Date.now());
      });
  }

  public readonly notifications = computed(() => {
    const filter = this.activeFilter();
    const all = this._store.notifications();
    if (filter === "All") {
      return all;
    }
    if (filter === "System") {
      return all.filter(n => n.type === "System" || n.type === "Connection" || n.type === "Error" || n.type === "Action");
    }
    return all.filter(n => n.type === filter);
  });

  public readonly filters: FilterOption[] = ["All", "Fill", "OrderUpdate", "System"];

  public onClose(): void {
    this.closed.emit();
  }

  public onOverlayClick(): void {
    this.onClose();
  }

  public onFilterChange(filter: FilterOption): void {
    this.activeFilter.set(filter);
  }

  public onMarkAllRead(): void {
    this._store.markAllRead();
  }

  public onClear(): void {
    this._store.clear();
  }

  public getIcon(type: NotificationType): string {
    switch (type) {
      case "Fill": return "swap_vert";
      case "OrderUpdate": return "receipt_long";
      case "Connection": return "wifi";
      case "System": return "terminal";
      case "Error": return "error_outline";
      case "Action": return "check_circle_outline";
    }
  }

  public getFilterLabel(filter: FilterOption): string {
    switch (filter) {
      case "All": return "All";
      case "Fill": return "Fills";
      case "OrderUpdate": return "Orders";
      case "System": return "System";
      case "Connection": return "Connection";
    }
  }

  public trackById(_index: number, notification: AppNotification): string {
    return notification.id;
  }
}
