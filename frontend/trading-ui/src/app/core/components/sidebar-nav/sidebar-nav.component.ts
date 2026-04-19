import { Component, Input, output } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { RouterLink, RouterLinkActive } from "@angular/router";

export interface NavItem {
  route: string;
  icon: string;
  label: string;
  exact?: boolean;
  feature?: string;
  upgradePrompt?: string;
}

@Component({
  selector: "app-sidebar-nav",
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule],
  templateUrl: "./sidebar-nav.component.html",
  styleUrl: "./sidebar-nav.component.scss"
})
export class SidebarNavComponent {
  @Input()
  public isAdmin = false;

  @Input()
  public features: string[] = [];

  public expanded = true;

  private readonly _baseNavItems: NavItem[] = [
    { route: "/dashboard", icon: "dashboard", label: "Dashboard", exact: true },
    { route: "/market-data", icon: "show_chart", label: "Market Data" },
    { route: "/strategies", icon: "tune", label: "Strategies", exact: true },
    { route: "/strategies/wizard", icon: "auto_fix_high", label: "Strategy Wizard" },
    { route: "/backtesting", icon: "history", label: "Backtesting" },
    { route: "/candle-data", icon: "candlestick_chart", label: "Data Management" },
    { route: "/agents", icon: "devices", label: "Agents" },
    { route: "/order-entry", icon: "swap_vert", label: "Order Entry" }
  ];

  private readonly _proNavItems: NavItem[] = [
    { route: "/optimizer", icon: "auto_graph", label: "Optimizer", feature: "optimizer", upgradePrompt: "optimizer" },
    { route: "/macro-calendar", icon: "event_note", label: "Macro Calendar", feature: "macrocalendar", upgradePrompt: "macro-calendar" },
    { route: "/settings/webhooks", icon: "hub", label: "Webhooks", feature: "webhooks", upgradePrompt: "webhooks" }
  ];

  private readonly _adminNavItems: NavItem[] = [
    { route: "/admin/strategy-library", icon: "admin_panel_settings", label: "Strategy Library" },
    { route: "/admin/users", icon: "manage_accounts", label: "Admin Users" }
  ];

  public get navItems(): NavItem[] {
    return this._baseNavItems;
  }

  public get proNavItems(): NavItem[] {
    return this._proNavItems;
  }

  public get adminNavItems(): NavItem[] {
    return this.isAdmin ? this._adminNavItems : [];
  }

  public get hasLockedProItems(): boolean {
    return this._proNavItems.some((item) => !this.hasFeature(item.feature));
  }

  public hasFeature(feature: string | undefined): boolean {
    if (!feature) {
      return true;
    }

    return this.features.some((item) => item.toLowerCase() === feature.toLowerCase());
  }

  public getUpgradeQueryParams(item: NavItem): Record<string, string> {
    return {
      upgrade: item.upgradePrompt ?? item.label.toLowerCase()
    };
  }

  public getTooltip(item: NavItem): string {
    if (this.expanded) {
      return "";
    }

    return this.hasFeature(item.feature)
      ? item.label
      : `${item.label} · Pro required`;
  }

  public readonly logoutClicked = output();

  public onToggle(): void {
    this.expanded = !this.expanded;
  }

  public onLogout(): void {
    this.logoutClicked.emit();
  }
}
