import { Routes } from "@angular/router";

export const routes: Routes = [
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