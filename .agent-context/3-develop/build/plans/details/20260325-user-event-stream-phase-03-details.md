<!-- markdownlint-disable-file -->

# Task Details: F7 — User Event Stream

## Phase 3: Frontend — Shared State Service & SignalR Integration

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, inject(), BehaviorSubject with $ suffix, takeUntilDestroyed, DTOs in `dtos/` folder with `.dto.ts` suffix, models in `models/` folder with `.model.ts` suffix, `@Injectable({ providedIn: "root" })`
- `.agent-context/0-knowledge/11-angular-instructions.md` — BehaviorSubject for service state, `merge(timer, refresh$)` polling pattern, CSS custom properties

## Design References

- `HealthService` BehaviorSubject pattern → `AccountStateService` pattern
- `SignalRService.priceUpdate$` Subject → new `fillEvent$` / `orderUpdate$` Subjects
- Connection status aggregation: extend `_emitConnectionStatus()` to include user event stream status source
- Existing connection status model already has `source`, `status`, `detail`, `retryCount` fields

### Task 3.1: Create Angular models and DTOs {#task-31-create-angular-models-and-dtos}

Create TypeScript interfaces for the SignalR event payloads and the activity feed event model.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/fill-event.model.ts` - new file
  - `frontend/trading-ui/src/app/core/models/order-update.model.ts` - new file
  - `frontend/trading-ui/src/app/core/models/user-event.model.ts` - new file (union type for activity feed)
- **Success**:
  - Interfaces compile and match the backend SignalR payload shapes
  - Activity feed event model is a discriminated union of fill and order update events
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/fill-event.model.ts — new file
export interface FillEvent {
  timestamp: string;
  asset: string;
  side: string;
  size: number;
  price: number;
  fee: number;
  orderId: string;
}
```

```typescript
// frontend/trading-ui/src/app/core/models/order-update.model.ts — new file
export interface OrderUpdate {
  timestamp: string;
  orderId: string;
  asset: string;
  status: string;
  filledSize: number;
  remainingSize: number;
}
```

```typescript
// frontend/trading-ui/src/app/core/models/user-event.model.ts — new file
import { FillEvent } from "./fill-event.model";
import { OrderUpdate } from "./order-update.model";

export type UserEventType = "Fill" | "OrderUpdate";

export interface UserEvent {
  type: UserEventType;
  timestamp: Date;
  data: FillEvent | OrderUpdate;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/price-update.model.ts` — existing SignalR payload interface pattern
- `frontend/trading-ui/src/app/core/models/connection-status.model.ts` — existing interface pattern

---

### Task 3.2: Create AccountStateService {#task-32-create-accountstateservice}

Create a shared Angular service holding positions, orders, and activity feed events as `BehaviorSubject` state. This service is the reactive state layer between SignalR events and dashboard components.

- **Complexity**: Medium
- **Risk Factors**: Must correctly merge SignalR push events with existing polling data; 100-event cap logic
- **Files**:
  - `frontend/trading-ui/src/app/core/services/account-state.service.ts` - new file
- **Success**:
  - Positions and orders state updated reactively from SignalR events
  - Activity feed events capped at 100, newest first
  - Exposed as `Observable`s for component consumption
- **Dependencies**: Task 3.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/account-state.service.ts — new file
import { Injectable } from "@angular/core";
import { BehaviorSubject, Observable } from "rxjs";
import { Position } from "../models/position.model";
import { OpenOrder } from "../models/open-order.model";
import { FillEvent } from "../models/fill-event.model";
import { OrderUpdate } from "../models/order-update.model";
import { UserEvent } from "../models/user-event.model";

@Injectable({ providedIn: "root" })
export class AccountStateService {
  private static readonly MAX_EVENTS = 100;

  private readonly _positions$ = new BehaviorSubject<Position[]>([]);
  public readonly positions$: Observable<Position[]> = this._positions$.asObservable();

  private readonly _orders$ = new BehaviorSubject<OpenOrder[]>([]);
  public readonly orders$: Observable<OpenOrder[]> = this._orders$.asObservable();

  private readonly _events$ = new BehaviorSubject<UserEvent[]>([]);
  public readonly events$: Observable<UserEvent[]> = this._events$.asObservable();

  public updatePositions(positions: Position[]): void {
    this._positions$.next(positions);
  }

  public updateOrders(orders: OpenOrder[]): void {
    this._orders$.next(orders);
  }

  public addFillEvent(fill: FillEvent): void {
    const event: UserEvent = {
      type: "Fill",
      timestamp: new Date(fill.timestamp),
      data: fill
    };
    this._addEvent(event);
  }

  public addOrderUpdateEvent(orderUpdate: OrderUpdate): void {
    const event: UserEvent = {
      type: "OrderUpdate",
      timestamp: new Date(orderUpdate.timestamp),
      data: orderUpdate
    };
    this._addEvent(event);
  }

  private _addEvent(event: UserEvent): void {
    const current = this._events$.getValue();
    const updated = [event, ...current].slice(0, AccountStateService.MAX_EVENTS);
    this._events$.next(updated);
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/health.service.ts` — BehaviorSubject → asObservable pattern, injectable root service

---

### Task 3.3: Extend SignalRService with user event handlers {#task-33-extend-signalrservice-with-user-event-handlers}

Extend the existing `SignalRService` to register handlers for the three new SignalR methods: `ReceiveFillEvent`, `ReceiveOrderUpdate`, `ReceiveUserConnectionStatus`. Wire events through to `AccountStateService`.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing price update and connection status handling
- **Files**:
  - `frontend/trading-ui/src/app/core/services/signalr.service.ts` - modification
- **Success**:
  - `ReceiveFillEvent` handler calls `AccountStateService.addFillEvent()`
  - `ReceiveOrderUpdate` handler calls `AccountStateService.addOrderUpdateEvent()`
  - `ReceiveUserConnectionStatus` handler updates the user event stream status
  - Existing `ReceivePriceUpdate` and `ReceiveConnectionStatus` handlers unchanged
- **Dependencies**: Tasks 3.1, 3.2

#### Implementation Details

Add to the SignalRService constructor (after existing event registrations):

```typescript
// frontend/trading-ui/src/app/core/services/signalr.service.ts — modification
// Add injection:
private readonly _accountState = inject(AccountStateService);

// Add user event stream status tracking field alongside existing _backendStatus:
private _userEventStatus: ConnectionStatus | null = null;

// Add new event handler registrations in constructor after existing handlers:
this._hubConnection.on("ReceiveFillEvent", (fill: FillEvent) => {
  this._accountState.addFillEvent(fill);
});

this._hubConnection.on("ReceiveOrderUpdate", (orderUpdate: OrderUpdate) => {
  this._accountState.addOrderUpdateEvent(orderUpdate);
});

this._hubConnection.on("ReceiveUserConnectionStatus", (status: ConnectionStatus) => {
  this._userEventStatus = status;
  this._emitConnectionStatus();
});
```

Add required imports:

```typescript
import { AccountStateService } from "./account-state.service";
import { FillEvent } from "../models/fill-event.model";
import { OrderUpdate } from "../models/order-update.model";
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/signalr.service.ts` — existing `hubConnection.on("ReceivePriceUpdate", ...)` and `hubConnection.on("ReceiveConnectionStatus", ...)` patterns

---

### Task 3.4: Update connection status aggregation {#task-34-update-connection-status-aggregation}

Modify the `_emitConnectionStatus()` method (or equivalent) in `SignalRService` to aggregate both the market data backend status and the user event stream status into a single worst-case connection status.

- **Complexity**: Medium
- **Risk Factors**: Must preserve existing connection status behaviour for F4 while adding F7 aggregation
- **Files**:
  - `frontend/trading-ui/src/app/core/services/signalr.service.ts` - modification (to `_emitConnectionStatus` or `_resolveMostSevereStatus`)
- **Success**:
  - When both streams connected: status = "Connected"
  - When one stream reconnecting: status = "Reconnecting"
  - When one stream disconnected: status = "Disconnected"
  - `detail` field shows which stream(s) are affected
- **Dependencies**: Task 3.3

#### Implementation Details

Extend the existing status resolution logic to consider all three sources: SignalR transport, market data backend status, and user event stream status. Chain two `_resolveMostSevereStatus` calls to correctly aggregate three sources without changing the existing method signature.

```typescript
// Extend _emitConnectionStatus to factor in _userEventStatus
// Chain two calls to the existing _resolveMostSevereStatus method
// to correctly aggregate all three status sources.

private _emitConnectionStatus(): void {
  // First resolve SignalR transport vs market data backend (existing behaviour)
  const afterBackend = this._resolveMostSevereStatus(this._signalRStatus, this._backendStatus);
  // Then resolve against user event stream status (new)
  const afterUserEvents = this._resolveMostSevereStatus(afterBackend, this._userEventStatus);
  this._connectionStatus$.next(afterUserEvents);
}
```

The exact implementation depends on the current `_emitConnectionStatus()` and `_resolveMostSevereStatus()` signatures. The implementer should read the existing logic and extend it to include the user event stream as an additional source, maintaining the same severity ordering: Disconnected > Reconnecting > Connected.

##### Pattern References

- `frontend/trading-ui/src/app/core/services/signalr.service.ts` — existing `_emitConnectionStatus()` and `_resolveMostSevereStatus()` methods

---

### Task 3.5: Run frontend build and lint {#task-35-run-frontend-build-and-lint}

Run the Angular build and lint to verify all changes compile and conform to project standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No new files
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npm run lint` passes with no violations
- **Dependencies**: Tasks 3.1–3.4

## Phase Success Criteria

- Angular models for FillEvent, OrderUpdate, and UserEvent exist and compile
- AccountStateService holds positions, orders, and events as BehaviorSubject state
- Activity feed events capped at 100, newest first, oldest discarded
- SignalRService handles ReceiveFillEvent, ReceiveOrderUpdate, ReceiveUserConnectionStatus
- Connection status indicator aggregates both market data and user event stream status
- Frontend builds and lints cleanly
