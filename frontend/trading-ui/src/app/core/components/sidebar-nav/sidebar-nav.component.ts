import { Component, Input, output } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { RouterLink, RouterLinkActive } from "@angular/router";

export interface NavItem {
  route: string;
  icon: string;
  label: string;
  exact?: boolean;
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
    { route: "/optimizer", icon: "auto_graph", label: "Optimizer" },
    { route: "/macro-calendar", icon: "event_note", label: "Macro Calendar" },
    { route: "/agents", icon: "devices", label: "Agents" },
    { route: "/order-entry", icon: "swap_vert", label: "Order Entry" }
  ];

  public get navItems(): NavItem[] {
    const normalizedFeatures = new Set(this.features.map((feature) => feature.toLowerCase()));
    const baseItems = this._baseNavItems.filter((item) => {
      if (item.route === "/optimizer") {
        return normalizedFeatures.has("optimizer");
      }

      if (item.route === "/macro-calendar") {
        return normalizedFeatures.has("macrocalendar");
      }

      return true;
    });

    if (!this.isAdmin) {
      return baseItems;
    }

    return [
      ...baseItems,
      { route: "/admin/strategy-library", icon: "admin_panel_settings", label: "Strategy Library" },
      { route: "/admin/users", icon: "manage_accounts", label: "Admin Users" }
    ];
  }

  public readonly logoutClicked = output();

  public onToggle(): void {
    this.expanded = !this.expanded;
  }

  public onLogout(): void {
    this.logoutClicked.emit();
  }
}
