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
    if (!this.isAdmin) {
      return this._baseNavItems;
    }

    return [
      ...this._baseNavItems,
      { route: "/admin/strategy-library", icon: "admin_panel_settings", label: "Strategy Library" }
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
