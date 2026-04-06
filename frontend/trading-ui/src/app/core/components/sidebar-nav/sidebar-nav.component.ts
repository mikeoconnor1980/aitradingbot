import { Component } from "@angular/core";
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
  public expanded = true;

  public readonly navItems: NavItem[] = [
    { route: "/dashboard", icon: "dashboard", label: "Dashboard", exact: true },
    { route: "/market-data", icon: "show_chart", label: "Market Data" },
    { route: "/strategies", icon: "tune", label: "Strategies" },
    { route: "/backtesting", icon: "history", label: "Backtesting" },
    { route: "/candle-data", icon: "candlestick_chart", label: "Candle Data" },
    { route: "/optimizer", icon: "auto_graph", label: "Optimizer" },
    { route: "/macro-calendar", icon: "event_note", label: "Macro Calendar" },
    { route: "/agents", icon: "devices", label: "Agents" },
    { route: "/order-entry", icon: "swap_vert", label: "Order Entry" }
  ];

  public onToggle(): void {
    this.expanded = !this.expanded;
  }
}
