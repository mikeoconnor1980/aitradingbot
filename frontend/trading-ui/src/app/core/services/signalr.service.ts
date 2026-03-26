import { Injectable, OnDestroy, inject } from "@angular/core";
import * as signalR from "@microsoft/signalr";
import { BehaviorSubject, Observable, Subject } from "rxjs";
import { environment } from "../../../environments/environment";
import { ConnectionState, ConnectionStatus } from "../models/connection-status.model";
import { PriceUpdate } from "../models/price-update.model";
import { AccountStateService } from "./account-state.service";
import { FillEvent } from "../models/fill-event.model";
import { OrderUpdate } from "../models/order-update.model";

@Injectable({ providedIn: "root" })
export class SignalRService implements OnDestroy {
  private readonly _accountState = inject(AccountStateService);

  private readonly _priceUpdate$ = new Subject<PriceUpdate>();
  private readonly _connectionStatus$ = new BehaviorSubject<ConnectionStatus>({
    source: "SignalR",
    status: "Disconnected",
    detail: null,
    retryCount: 0
  });

  private readonly _signalRStatus: ConnectionStatus = {
    source: "SignalR",
    status: "Disconnected",
    detail: null,
    retryCount: 0
  };
  private _backendStatus: ConnectionStatus | null = null;
  private _userEventStatus: ConnectionStatus | null = null;
  private _signalRRetryCount = 0;
  private readonly _hubConnection: signalR.HubConnection;

  public readonly priceUpdate$: Observable<PriceUpdate> = this._priceUpdate$.asObservable();
  public readonly connectionStatus$: Observable<ConnectionStatus> = this._connectionStatus$.asObservable();

  public constructor() {
    this._hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubBaseUrl)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this._registerHandlers();
    void this._startConnection();
  }

  public ngOnDestroy(): void {
    void this._hubConnection.stop();
    this._priceUpdate$.complete();
    this._connectionStatus$.complete();
  }

  private _registerHandlers(): void {
    this._hubConnection.on("ReceivePriceUpdate", (update: PriceUpdate) => {
      this._priceUpdate$.next(update);
    });

    this._hubConnection.on("ReceiveConnectionStatus", (status: ConnectionStatus) => {
      this._backendStatus = status;
      this._emitConnectionStatus();
    });

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

    this._hubConnection.onreconnecting((error?: Error) => {
      this._signalRRetryCount += 1;
      this._signalRStatus.status = "Reconnecting";
      this._signalRStatus.detail = error?.message ?? null;
      this._signalRStatus.retryCount = this._signalRRetryCount;
      this._emitConnectionStatus();
    });

    this._hubConnection.onreconnected(() => {
      this._signalRRetryCount = 0;
      this._signalRStatus.status = "Connected";
      this._signalRStatus.detail = null;
      this._signalRStatus.retryCount = 0;
      this._emitConnectionStatus();
    });

    this._hubConnection.onclose((error?: Error) => {
      this._signalRStatus.status = "Disconnected";
      this._signalRStatus.detail = error?.message ?? null;
      this._signalRStatus.retryCount = this._signalRRetryCount;
      this._emitConnectionStatus();
    });
  }

  private static readonly INITIAL_RETRY_DELAYS_MS = [0, 1000, 2000, 5000, 10000, 30000];

  private async _startConnection(): Promise<void> {
    for (const delay of SignalRService.INITIAL_RETRY_DELAYS_MS) {
      if (delay > 0) {
        await new Promise(resolve => setTimeout(resolve, delay));
      }
      try {
        await this._hubConnection.start();
        this._signalRRetryCount = 0;
        this._signalRStatus.status = "Connected";
        this._signalRStatus.detail = null;
        this._signalRStatus.retryCount = 0;
        this._emitConnectionStatus();
        return;
      } catch (error: unknown) {
        this._signalRRetryCount += 1;
        this._signalRStatus.status = "Reconnecting";
        this._signalRStatus.detail = error instanceof Error ? error.message : "Unknown SignalR connection error";
        this._signalRStatus.retryCount = this._signalRRetryCount;
        this._emitConnectionStatus();
      }
    }
    this._signalRStatus.status = "Disconnected";
    this._signalRStatus.detail = "Initial connection failed after all retry attempts";
    this._emitConnectionStatus();
  }

  private _emitConnectionStatus(): void {
    const afterBackend = this._resolveMostSevereStatus(this._signalRStatus, this._backendStatus);
    const afterUserEvents = this._resolveMostSevereStatus(afterBackend, this._userEventStatus);
    this._connectionStatus$.next(afterUserEvents);
  }

  private _resolveMostSevereStatus(primary: ConnectionStatus, secondary: ConnectionStatus | null): ConnectionStatus {
    if (secondary === null) {
      return { ...primary };
    }

    return this._severity(primary.status) >= this._severity(secondary.status)
      ? { ...primary }
      : { ...secondary };
  }

  private _severity(status: ConnectionState): number {
    switch (status) {
      case "Disconnected":
        return 2;
      case "Reconnecting":
        return 1;
      case "Connected":
      default:
        return 0;
    }
  }
}
