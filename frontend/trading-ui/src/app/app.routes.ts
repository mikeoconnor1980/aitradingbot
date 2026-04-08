import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";
import { unsavedChangesGuard } from "./features/strategy-builder/guards/unsaved-changes.guard";

export const routes: Routes = [
  {
    path: "login",
    loadComponent: () => import("./features/auth/login-page.component").then((m) => m.LoginPageComponent),
    title: "Sign In"
  },
  {
    path: "register",
    loadComponent: () => import("./features/auth/register-page.component").then((m) => m.RegisterPageComponent),
    title: "Register"
  },
  {
    path: "strategies",
    loadComponent: () => import("./features/strategy-builder/strategy-list-page.component").then((m) => m.StrategyListPageComponent),
    canActivate: [authGuard],
    title: "Strategies"
  },
  {
    path: "strategies/new",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canActivate: [authGuard],
    canDeactivate: [unsavedChangesGuard],
    title: "New Strategy"
  },
  {
    path: "strategies/:id/edit",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canActivate: [authGuard],
    canDeactivate: [unsavedChangesGuard],
    title: "Edit Strategy"
  },
  {
    path: "market-data",
    loadComponent: () => import("./features/market-data/market-data.component").then((m) => m.MarketDataComponent),
    canActivate: [authGuard],
    title: "Market Data"
  },
  {
    path: "dashboard",
    loadComponent: () => import("./features/dashboard/dashboard.component").then((m) => m.DashboardComponent),
    canActivate: [authGuard]
  },
  {
    path: "connection",
    loadComponent: () => import("./features/connection/status-card.component").then((m) => m.StatusCardComponent),
    canActivate: [authGuard]
  },
  {
    path: "order-entry",
    loadComponent: () => import("./features/order-entry/order-entry.component").then((m) => m.OrderEntryComponent),
    canActivate: [authGuard],
    title: "Order Entry"
  },
  {
    path: "backtesting",
    loadComponent: () => import("./features/backtesting/backtest-page.component").then((m) => m.BacktestPageComponent),
    canActivate: [authGuard],
    title: "Backtesting"
  },
  {
    path: "candle-data",
    loadComponent: () => import("./features/candle-management/candle-management.component").then((m) => m.CandleManagementComponent),
    canActivate: [authGuard],
    title: "Candle Data"
  },
  {
    path: "optimizer",
    loadComponent: () => import("./features/optimizer/optimizer-page.component").then((m) => m.OptimizerPageComponent),
    canActivate: [authGuard],
    title: "Strategy Optimizer"
  },
  {
    path: "agents",
    loadComponent: () => import("./features/agents/agents-page.component").then((m) => m.AgentsPageComponent),
    canActivate: [authGuard],
    title: "Agents"
  },
  {
    path: "profile",
    loadComponent: () => import("./features/profile/profile-page.component").then((m) => m.ProfilePageComponent),
    canActivate: [authGuard],
    title: "Profile"
  },
  {
    path: "macro-calendar",
    loadComponent: () => import("./features/macro-calendar/macro-calendar-page.component").then((m) => m.MacroCalendarPageComponent),
    canActivate: [authGuard],
    title: "Macro Calendar"
  },
  { path: "", redirectTo: "dashboard", pathMatch: "full" },
  { path: "**", redirectTo: "dashboard" }
];