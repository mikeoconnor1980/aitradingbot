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

export interface NavGroup {
  label: string;
  items: NavItem[];
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

  public readonly logoutClicked = output();

  public get navGroups(): NavGroup[] {
    const operateItems: NavItem[] = [
      { route: "/candle-data", icon: "database", label: "Data Management" },
      { route: "/connection", icon: "lan", label: "Connection" }
    ];

    if (this.isAdmin) {
      operateItems.push(
        { route: "/admin/strategy-library", icon: "admin_panel_settings", label: "Strategy Library" },
        { route: "/admin/users", icon: "manage_accounts", label: "Admin Users" }
      );
    }

    return [
      {
        label: "Monitor",
        items: [
          { route: "/dashboard", icon: "space_dashboard", label: "Overview", exact: true },
          { route: "/market-data", icon: "candlestick_chart", label: "Markets" },
          { route: "/analyst", icon: "psychology", label: "Analyst" },
          { route: "/macro-calendar", icon: "event_note", label: "Macro Calendar", feature: "macrocalendar", upgradePrompt: "macro-calendar" }
        ]
      },
      {
        label: "Build & Research",
        items: [
          { route: "/strategies", icon: "tune", label: "Strategies", exact: true },
          { route: "/strategies/wizard", icon: "auto_fix_high", label: "Strategy Wizard" },
          { route: "/backtesting", icon: "history", label: "Backtests" },
          { route: "/optimizer", icon: "auto_graph", label: "Optimizer", feature: "optimizer", upgradePrompt: "optimizer" }
        ]
      },
      {
        label: "Execute & Automate",
        items: [
          { route: "/order-entry", icon: "swap_vert", label: "Order Entry" },
          { route: "/agents", icon: "devices", label: "Agents" },
          { route: "/settings/webhooks", icon: "hub", label: "Webhooks", feature: "webhooks", upgradePrompt: "webhooks" }
        ]
      },
      { label: "Operate", items: operateItems }
    ];
  }

  public hasFeature(feature: string | undefined): boolean {
    if (!feature) {
      return true;
    }

    return this.features.some((item) => item.toLowerCase() === feature.toLowerCase());
  }

  public getUpgradeQueryParams(item: NavItem): Record<string, string> {
    return { upgrade: item.upgradePrompt ?? item.label.toLowerCase() };
  }

  public getTooltip(item: NavItem): string {
    if (this.expanded) {
      return "";
    }

    return this.hasFeature(item.feature) ? item.label : `${item.label} · Pro required`;
  }

  public onToggle(): void {
    this.expanded = !this.expanded;
  }

  public onLogout(): void {
    this.logoutClicked.emit();
  }
}
