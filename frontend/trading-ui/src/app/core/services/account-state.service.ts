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

  public seedFillEvents(fills: FillEvent[]): void {
    const current = this._events$.getValue();
    if (current.length > 0) {
      return; // Already have events from WebSocket, don't overwrite
    }

    const events: UserEvent[] = fills.map(fill => ({
      type: "Fill" as const,
      timestamp: new Date(fill.timestamp),
      data: fill
    }));

    this._events$.next(events.slice(0, AccountStateService.MAX_EVENTS));
  }

  private _addEvent(event: UserEvent): void {
    const current = this._events$.getValue();
    const updated = [event, ...current].slice(0, AccountStateService.MAX_EVENTS);
    this._events$.next(updated);
  }
}
