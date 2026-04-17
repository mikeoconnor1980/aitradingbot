<!-- markdownlint-disable-file -->

# Task Details: Adaptive Risk (Drawdown-Adjusted)

## Phase 4: API Endpoint & Frontend Dashboard

## Standards and Knowledge References

- `.github/instructions/api-controllers.instructions.md` — controller structure, routes, ProducesResponseType
- `.github/instructions/angular.instructions.md` — standalone components, inject(), takeUntilDestroyed, SCSS BEM, double quotes
- `.github/instructions/csharp.instructions.md` — sealed records, async patterns
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — dashboard display requirements

## Design References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/` — tiered threshold indicator (direct template for drawdown indicator)
- `frontend/trading-ui/src/app/features/connection/status-card.component.ts` — badge pattern for CB status
- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — parent card for dashboard indicators

---

### Task 4.1: Create DrawdownStateResponse DTO and API endpoint {#task-41-create-drawdownstateresponse-dto-and-api-endpoint}

Add a new `GET /api/risk/drawdown-state` endpoint to `RiskController` that returns the current drawdown state.

- **Complexity**: Medium
- **Risk Factors**: `LiveRiskEngine` is singleton in Worker but scoped in Api — the Api needs access to the live drawdown state. If the API runs separately from the Worker, it may not have real-time drawdown state. The implementing agent should verify if `IRiskEngine` is available in the API DI container and what its lifetime is.
- **Files**:
  - `src/TradePilot.Application/Trading/Models/DrawdownStateResponse.cs` — new file
  - `src/TradePilot.Api/Controllers/RiskController.cs` — add endpoint
- **Success**:
  - `DrawdownStateResponse` DTO with `DrawdownPercent`, `HighWaterMark`, `ScalingFactor`, `IsCircuitBreakerActive`
  - `GET /api/risk/drawdown-state` endpoint returns the current state
  - Endpoint follows existing `RiskController` pattern (direct injection, no MediatR)
- **Dependencies**: Phase 2

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Models/DrawdownStateResponse.cs — new file
namespace TradePilot.Application.Trading.Models;

public sealed record DrawdownStateResponse
{
    public required decimal DrawdownPercent { get; init; }
    public required decimal HighWaterMark { get; init; }
    public required decimal ScalingFactor { get; init; }
    public required bool IsCircuitBreakerActive { get; init; }
}
```

```csharp
// src/TradePilot.Api/Controllers/RiskController.cs — add endpoint
// NOTE: The API and Worker are separate processes. LiveRiskEngine is NOT available in the API.
// Compute drawdown state on-demand from the strategy's persisted HWM + exchange equity.
// Inject IStrategyRepository (or the existing repository pattern) in the constructor.

[HttpGet("drawdown-state")]
[ProducesResponseType(typeof(DrawdownStateResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> GetDrawdownStateAsync(CancellationToken cancellationToken)
{
    var address = await GetWalletAddressAsync(cancellationToken);
    if (address is null)
        return ServiceUnavailable("No wallet address configured.");

    var accountState = await _accountService.GetAccountStateAsync(address, cancellationToken);
    var equity = accountState.MarginSummary.AccountValue;

    // Fetch the active strategy's persisted HWM from the database
    var strategy = await _strategyRepository.GetActiveStrategyAsync(userId, cancellationToken);
    var hwm = strategy?.HighWaterMarkUsd ?? equity;

    var result = DrawdownEvaluator.Evaluate(equity, hwm, _limits.DrawdownTiers);

    return Ok(new DrawdownStateResponse
    {
        DrawdownPercent = result.DrawdownPercent,
        HighWaterMark = result.NewHighWaterMark,
        ScalingFactor = result.ScalingFactor,
        IsCircuitBreakerActive = result.IsHalted,
    });
}
```

Note: The implementing agent should inspect `RiskController`'s existing constructor and `GetWalletAddressAsync` helper. Add `IStrategyRepository` (or equivalent) to the constructor. The `userId` should be extracted from the JWT claims following the existing pattern in the controller (see `GetPortfolioHeatAsync`). `DrawdownEvaluator` is a static utility and needs no DI.

##### Pattern References

- `src/TradePilot.Api/Controllers/RiskController.cs` — existing `GetPortfolioHeatAsync` endpoint
- `src/TradePilot.Application/Trading/Models/PortfolioHeatResponse.cs` — existing response DTO pattern

---

### Task 4.2: Create DrawdownState frontend model and API service method {#task-42-create-drawdownstate-frontend-model-and-api-service-method}

Create the TypeScript model and add a method to the API service to fetch drawdown state.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/drawdown-state.model.ts` — new file
  - `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — add method
- **Success**:
  - `DrawdownState` interface matches backend DTO (camelCase)
  - `getDrawdownState()` method added to API service
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/drawdown-state.model.ts — new file
export interface DrawdownState {
  drawdownPercent: number;
  highWaterMark: number;
  scalingFactor: number;
  isCircuitBreakerActive: boolean;
}
```

```typescript
// frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts — add method
// Follow existing getPortfolioHeat() pattern:
public getDrawdownState(): Observable<DrawdownState> {
  return this._http.get<DrawdownState>(`${this._baseUrl}/risk/drawdown-state`);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/portfolio-heat.model.ts` — existing model pattern
- `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — existing `getPortfolioHeat()` method

---

### Task 4.3: Create DrawdownIndicatorComponent {#task-43-create-drawdownindicatorcomponent}

Create a new standalone Angular component that displays drawdown %, active scaling factor, and circuit breaker status. Follow the `PortfolioHeatIndicatorComponent` pattern.

- **Complexity**: Medium
- **Risk Factors**: CSS tier colours must align with existing design system tokens
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.ts` — new file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.html` — new file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.scss` — new file
- **Success**:
  - Component accepts `@Input() drawdownState: DrawdownState | null`
  - Displays current drawdown % with progress bar
  - Shows active scaling factor (e.g. "75% risk" or "Halted")
  - Shows CB status badge (active/inactive)
  - Uses tiered CSS classes: `--low` (0-5%), `--elevated` (5-10%), `--critical` (10-15%), `--halted` (15%+)
  - Pulse animation on `--halted` state
  - Standalone component
- **Dependencies**: Task 4.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.ts — new file
import { Component, Input } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatIconModule } from "@angular/material/icon";
import { DrawdownState } from "../../../../core/models/drawdown-state.model";

@Component({
  selector: "app-drawdown-indicator",
  standalone: true,
  imports: [CommonModule, MatProgressBarModule, MatIconModule],
  templateUrl: "./drawdown-indicator.component.html",
  styleUrls: ["./drawdown-indicator.component.scss"],
})
export class DrawdownIndicatorComponent {
  @Input() drawdownState: DrawdownState | null = null;

  public get barValue(): number {
    if (!this.drawdownState) return 0;
    return Math.min(this.drawdownState.drawdownPercent, 20); // Cap at 20% for bar display
  }

  public get threshold(): { cssClass: string; label: string } {
    if (!this.drawdownState) return { cssClass: "low", label: "No data" };

    const dd = this.drawdownState.drawdownPercent;
    if (this.drawdownState.isCircuitBreakerActive) return { cssClass: "halted", label: "HALTED" };
    if (dd >= 10) return { cssClass: "critical", label: `${(this.drawdownState.scalingFactor * 100).toFixed(0)}% risk` };
    if (dd >= 5) return { cssClass: "elevated", label: `${(this.drawdownState.scalingFactor * 100).toFixed(0)}% risk` };
    return { cssClass: "low", label: "Full risk" };
  }

  public get isHalted(): boolean {
    return this.drawdownState?.isCircuitBreakerActive ?? false;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.html — new file -->
<div class="drawdown-indicator" [class.drawdown-indicator--halted]="isHalted">
  <div class="drawdown-indicator__header">
    <span class="drawdown-indicator__label">Drawdown</span>
    <span class="drawdown-indicator__value">{{ drawdownState?.drawdownPercent | number:'1.1-1' }}%</span>
    @if (isHalted) {
      <mat-icon class="drawdown-indicator__warning-icon">warning</mat-icon>
    }
  </div>
  <mat-progress-bar
    mode="determinate"
    [value]="barValue * 5"
    [class]="'drawdown-indicator__bar drawdown-indicator__bar--' + threshold.cssClass">
  </mat-progress-bar>
  <div class="drawdown-indicator__footer">
    <span class="drawdown-indicator__tier">{{ threshold.label }}</span>
    <span class="drawdown-indicator__badge"
          [class.drawdown-indicator__badge--active]="isHalted"
          [class.drawdown-indicator__badge--inactive]="!isHalted">
      <span class="drawdown-indicator__badge-dot"></span>
      {{ isHalted ? 'CB Active' : 'Normal' }}
    </span>
  </div>
</div>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.scss — new file
.drawdown-indicator {
  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 4px;
  }

  &__label {
    font-size: 0.85rem;
    color: var(--colour-text-secondary);
  }

  &__value {
    font-weight: 600;
    margin-left: auto;
  }

  &__warning-icon {
    color: var(--colour-loss);
    font-size: 18px;
    width: 18px;
    height: 18px;
  }

  &__bar {
    &--low ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-profit);
    }

    &--elevated ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-warning);
    }

    &--critical ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-warning-elevated, var(--colour-warning));
    }

    &--halted ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-loss);
    }
  }

  &__footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 4px;
    font-size: 0.75rem;
  }

  &__tier {
    color: var(--colour-text-secondary);
  }

  &__badge {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 2px 8px;
    border-radius: 12px;
    font-size: 0.7rem;
    font-weight: 500;

    &--active {
      color: var(--colour-loss);
      background: rgba(239, 83, 80, 0.1);
    }

    &--inactive {
      color: var(--colour-profit);
      background: rgba(59, 201, 168, 0.1);
    }
  }

  &__badge-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: currentColor;
  }

  &--halted {
    animation: pulse-warning 2s ease-in-out infinite;
  }
}

@keyframes pulse-warning {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/` — tiered indicator with progress bar, threshold logic, warning icon, pulse animation
- `frontend/trading-ui/src/app/features/connection/status-card.component.scss` — badge with dot pattern

---

### Task 4.4: Wire DrawdownIndicatorComponent into AccountSummary dashboard {#task-44-wire-drawdownindicatorcomponent-into-accountsummary-dashboard}

Add `DrawdownState` as an input to `AccountSummaryComponent` and render the `DrawdownIndicatorComponent`. Add polling in the parent dashboard component.

- **Complexity**: Medium
- **Risk Factors**: Must follow existing polling pattern; data fetched alongside portfolio heat
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — add input + import
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html` — add component
  - Parent dashboard component (wherever `AccountSummaryComponent` is instantiated) — add polling + pass input
- **Success**:
  - `AccountSummaryComponent` accepts `@Input() drawdownState: DrawdownState | null`
  - `DrawdownIndicatorComponent` rendered in the metrics grid alongside existing indicators
  - Parent dashboard polls `getDrawdownState()` on interval (matching existing pattern, e.g. 30s)
  - Drawdown data flows from API → parent → AccountSummary → DrawdownIndicator
- **Dependencies**: Tasks 4.2, 4.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts — modification
// Add to imports array:
import { DrawdownIndicatorComponent } from "./drawdown-indicator/drawdown-indicator.component";
import { DrawdownState } from "../../../core/models/drawdown-state.model";

// Add to component imports:
imports: [..., DrawdownIndicatorComponent],

// Add input:
@Input() drawdownState: DrawdownState | null = null;
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html — add after existing indicators -->
<app-drawdown-indicator [drawdownState]="drawdownState"></app-drawdown-indicator>
```

The implementing agent should locate the parent component that hosts `AccountSummaryComponent` (likely `DashboardComponent`) and add the drawdown polling alongside the existing portfolio heat polling, following the same `interval` + `switchMap` + `catchError` pattern.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — existing `@Input() portfolioHeat` pattern
- `frontend/trading-ui/src/app/features/dashboard/market-context-card/market-context-card.component.ts` — `interval(60_000)` polling pattern

---

### Task 4.5: Frontend build and lint {#task-45-frontend-build-and-lint}

Run the Angular build and lint to verify no issues.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `npm run build` (or `npx ng build`) succeeds with no errors
  - `npm run lint` succeeds with no errors
- **Dependencies**: Tasks 4.1–4.4

## Phase Success Criteria

- `GET /api/risk/drawdown-state` returns current drawdown %, HWM, scaling factor, and CB status
- `DrawdownIndicatorComponent` renders with correct tiered styling
- Dashboard displays real-time drawdown state alongside existing portfolio heat indicator
- CB status badge shows active (red) or inactive (green) state
- Pulse animation on halted state
- Frontend builds and lints cleanly
