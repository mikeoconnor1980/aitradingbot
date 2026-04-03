import { Routes } from "@angular/router";
import { unsavedChangesGuard } from "./features/strategy-builder/guards/unsaved-changes.guard";

export const routes: Routes = [
  {
    path: "strategies",
    loadComponent: () => import("./features/strategy-builder/strategy-list-page.component").then((m) => m.StrategyListPageComponent),
    title: "Strategies"
  },
  {
    path: "strategies/new",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canDeactivate: [unsavedChangesGuard],
    title: "New Strategy"
  },
  {
    path: "strategies/:id/edit",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canDeactivate: [unsavedChangesGuard],
    title: "Edit Strategy"
  },
  {
    path: "market-data",
    loadComponent: () => import("./features/market-data/market-data.component").then((m) => m.MarketDataComponent),
    title: "Market Data"
  },
  {
    path: "dashboard",
    loadComponent: () => import("./features/dashboard/dashboard.component").then((m) => m.DashboardComponent)
  },
  {
    path: "connection",
    loadComponent: () => import("./features/connection/status-card.component").then((m) => m.StatusCardComponent)
  },
  {
    path: "order-entry",
    loadComponent: () => import("./features/order-entry/order-entry.component").then((m) => m.OrderEntryComponent),
    title: "Order Entry"
  },
  {
    path: "backtesting",
    loadComponent: () => import("./features/backtesting/backtest-page.component").then((m) => m.BacktestPageComponent),
    title: "Backtesting"
  },
  { path: "", redirectTo: "dashboard", pathMatch: "full" },
  { path: "**", redirectTo: "dashboard" }
];