import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";
import { adminRoleGuard } from "./core/guards/admin-role.guard";
import { mobileRedirectGuard } from "./core/guards/mobile-redirect.guard";
import { subscriptionGuard, tierFeatureGuard } from "./core/guards/subscription.guard";
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
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    title: "Strategies"
  },
  {
    path: "strategies/new",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    canDeactivate: [unsavedChangesGuard],
    title: "New Strategy"
  },
  {
    path: "strategies/wizard",
    loadComponent: () => import("./features/strategy-builder/wizard/strategy-wizard-page.component").then((m) => m.StrategyWizardPageComponent),
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    title: "Strategy Wizard"
  },
  {
    path: "strategies/:id/edit",
    loadComponent: () => import("./features/strategy-builder/strategy-builder-page.component").then((m) => m.StrategyBuilderPageComponent),
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    canDeactivate: [unsavedChangesGuard],
    title: "Edit Strategy"
  },
  {
    path: "admin/strategy-library",
    loadComponent: () => import("./features/admin/strategy-library-page.component").then((m) => m.StrategyLibraryPageComponent),
    canActivate: [authGuard, adminRoleGuard, mobileRedirectGuard],
    title: "Strategy Library"
  },
  {
    path: "admin/users",
    loadComponent: () => import("./features/admin/admin-users-page.component").then((m) => m.AdminUsersPageComponent),
    canActivate: [authGuard, adminRoleGuard, mobileRedirectGuard],
    title: "Admin Users"
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
    path: "analyst",
    loadComponent: () => import("./features/analyst/analyst-page.component").then((m) => m.AnalystPageComponent),
    canActivate: [authGuard],
    title: "TradePilot Analyst"
  },
  {
    path: "connection",
    loadComponent: () => import("./features/connection/status-card.component").then((m) => m.StatusCardComponent),
    canActivate: [authGuard, mobileRedirectGuard]
  },
  {
    path: "order-entry",
    loadComponent: () => import("./features/order-entry/order-entry.component").then((m) => m.OrderEntryComponent),
    canActivate: [authGuard, subscriptionGuard],
    title: "Order Entry"
  },
  {
    path: "backtesting",
    loadComponent: () => import("./features/backtesting/backtest-page.component").then((m) => m.BacktestPageComponent),
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    title: "Backtesting"
  },
  {
    path: "candle-data",
    loadComponent: () => import("./features/data-management/data-management.component").then((m) => m.DataManagementComponent),
    canActivate: [authGuard, mobileRedirectGuard],
    title: "Data Management"
  },
  {
    path: "optimizer",
    loadComponent: () => import("./features/optimizer/optimizer-page.component").then((m) => m.OptimizerPageComponent),
    canActivate: [authGuard, subscriptionGuard, tierFeatureGuard("Optimizer"), mobileRedirectGuard],
    title: "Strategy Optimizer"
  },
  {
    path: "agents",
    loadComponent: () => import("./features/agents/agents-page.component").then((m) => m.AgentsPageComponent),
    canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard],
    title: "Agents"
  },
  {
    path: "settings/webhooks",
    loadComponent: () => import("./features/webhooks/webhooks-page.component").then((m) => m.WebhooksPageComponent),
    canActivate: [authGuard, subscriptionGuard, tierFeatureGuard("Webhooks"), mobileRedirectGuard],
    title: "TradingView Webhooks"
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
    canActivate: [authGuard, subscriptionGuard, tierFeatureGuard("MacroCalendar"), mobileRedirectGuard],
    title: "Macro Calendar"
  },
  { path: "", redirectTo: "dashboard", pathMatch: "full" },
  { path: "**", redirectTo: "dashboard" }
];