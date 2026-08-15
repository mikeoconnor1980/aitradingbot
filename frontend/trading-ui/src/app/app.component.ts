import { CommonModule } from "@angular/common";
import { Component, DestroyRef, OnInit, Type, ViewChild, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatMenuModule } from "@angular/material/menu";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { RouterLink, RouterOutlet } from "@angular/router";
import { HelpPanelComponent } from "./core/components/help-panel.component";
import { MobileNavComponent } from "./core/components/mobile-nav/mobile-nav.component";
import { NotificationPanelComponent } from "./core/components/notification-panel/notification-panel.component";
import { SidebarNavComponent } from "./core/components/sidebar-nav/sidebar-nav.component";
import { ConnectionStatus } from "./core/models/connection-status.model";
import { HealthResponse } from "./core/models/health-response.model";
import { AuthUser } from "./core/models/auth.model";
import { AuthService } from "./core/services/auth.service";
import { HealthService } from "./core/services/health.service";
import { HelpService } from "./core/services/help.service";
import { LayoutService } from "./core/services/layout.service";
import { NotificationStoreService } from "./core/services/notification-store.service";
import { ProfileService } from "./core/services/profile.service";
import { SubscriptionService } from "./core/services/subscription.service";
import { SignalRService } from "./core/services/signalr.service";
import { RightPanelService } from "./core/services/right-panel.service";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule, MatIconModule, MatButtonModule, MatTooltipModule, MatFormFieldModule, MatMenuModule, MatSelectModule, HelpPanelComponent, NotificationPanelComponent, SidebarNavComponent, MobileNavComponent],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss"
})
export class AppComponent implements OnInit {
  private readonly _signalRService = inject(SignalRService);
  private readonly _healthService = inject(HealthService);
  private readonly _helpService = inject(HelpService);
  private readonly _authService = inject(AuthService);
  private readonly _profileService = inject(ProfileService);
  private readonly _subscriptionService = inject(SubscriptionService);
  private readonly _layoutService = inject(LayoutService);
  private readonly _notificationStore = inject(NotificationStoreService);
  private readonly _rightPanels = inject(RightPanelService);
  private readonly _destroyRef = inject(DestroyRef);

  public title = "TradePilot";
  public currentUser: AuthUser | null = null;
  public isAuthenticated = false;
  public readonly isMobile = this._layoutService.isMobile;

  @ViewChild(SidebarNavComponent)
  public sidebar?: SidebarNavComponent;

  public connectionStatus: ConnectionStatus = {
    source: "SignalR",
    status: "Disconnected",
    detail: null,
    retryCount: 0
  };
  public health: HealthResponse | null = null;
  public preferredNetwork: string | null = null;
  public preferredExchange = "Hyperliquid";
  public exchangeSaving = false;
  public subscriptionFeatures: string[] = [];
  public readonly unreadCount = this._notificationStore.unreadCount;
  public readonly activeRightPanel = this._rightPanels.activePanel;
  public readonly analystPanelComponent = signal<Type<unknown> | null>(null);

  public ngOnInit(): void {
    this._signalRService.connectionStatus$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((status: ConnectionStatus) => {
        this.connectionStatus = status;
      });

    this._healthService.health$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((health: HealthResponse | null) => {
        this.health = health;
      });

    this._authService.user$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((user) => {
        this.currentUser = user;
      });

    this._authService.isAuthenticated$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((auth) => {
        this.isAuthenticated = auth;
        if (auth) {
          this._profileService.load();
          this._subscriptionService.loadStatus();
          this._authService.syncCurrentUser().subscribe({
            error: () => undefined
          });
        }
      });

    this._profileService.profile$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((profile) => {
        this.preferredNetwork = profile?.preferredNetwork ?? null;
        this.preferredExchange = profile?.preferredExchange ?? "Hyperliquid";
      });

    this._subscriptionService.status$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((status) => {
        this.subscriptionFeatures = status?.features ?? [];
      });
  }

  public get statusClass(): string {
    if (this.health !== null) {
      return this.health.status === "connected" ? "status--connected" : "status--disconnected";
    }

    switch (this.connectionStatus.status) {
      case "Connected":
        return "status--connected";
      case "Reconnecting":
        return "status--reconnecting";
      case "Disconnected":
      default:
        return "status--disconnected";
    }
  }

  public get networkLabel(): string | null {
    const network = this.preferredNetwork?.trim().toLowerCase();
    if (network === "testnet") return "Testnet";
    if (network === "mainnet") return "Mainnet";
    return null;
  }

  public onExchangeChange(exchange: string): void {
    if (!exchange || exchange === this.preferredExchange || this.exchangeSaving) {
      return;
    }

    this.exchangeSaving = true;
    this._profileService.updateExchange(exchange).subscribe({
      next: () => {
        this.exchangeSaving = false;
      },
      error: () => {
        this.exchangeSaving = false;
      }
    });
  }

  public get statusLabel(): string {
    if (this.health !== null) {
      return this.health.status === "connected" ? "Connected" : "Disconnected";
    }

    return this.connectionStatus.status;
  }

  public onToggleHelp(): void {
    this._rightPanels.toggle("help");
    if (this._rightPanels.activePanel() === "help") {
      this._helpService.toggle();
    }
  }

  public onToggleNotifications(): void {
    this._rightPanels.toggle("notifications");
    if (this._rightPanels.activePanel() === "notifications") {
      this._notificationStore.markAllRead();
    }
  }

  public onNotificationPanelClosed(): void {
    this._rightPanels.close("notifications");
  }

  public onToggleAnalyst(): void {
    this._helpService.close();
    this._rightPanels.toggle("analyst");
    if (this._rightPanels.activePanel() === "analyst" && !this.analystPanelComponent()) {
      void import("./features/analyst/analyst-panel.component").then((module) => this.analystPanelComponent.set(module.AnalystPanelComponent));
    }
  }

  public onLogout(): void {
    this._authService.logout();
  }
}
