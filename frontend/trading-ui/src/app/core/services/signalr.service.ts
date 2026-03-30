import { Injectable, OnDestroy, inject } from "@angular/core";
import * as signalR from "@microsoft/signalr";
import { BehaviorSubject, Observable, Subject } from "rxjs";
import { environment } from "../../../environments/environment";
import { BacktestProgress } from "../models/backtest.model";
import { ConnectionStatus } from "../models/connection-status.model";
import { PriceUpdate } from "../models/price-update.model";
import { AccountStateService } from "./account-state.service";
import { FillEvent } from "../models/fill-event.model";
import { OrderUpdate } from "../models/order-update.model";

@Injectable({ providedIn: "root" })
export class SignalRService implements OnDestroy {
  private static readonly DISCONNECTED_STATUS: ConnectionStatus = {
    source: "SignalR",
    status: "Disconnected",
    detail: null,
    retryCount: 0
  };

  private readonly _accountState = inject(AccountStateService);

  private readonly _priceUpdate$ = new Subject<PriceUpdate>();
  private readonly _backtestProgress$ = new Subject<BacktestProgress>();
  private readonly _connectionStatus$ = new BehaviorSubject<ConnectionStatus>(SignalRService.DISCONNECTED_STATUS);
  private readonly _transportConnectionStatus$ = new BehaviorSubject<ConnectionStatus>(SignalRService.DISCONNECTED_STATUS);
  private readonly _statusBySource = new Map<string, ConnectionStatus>([["SignalR", SignalRService.DISCONNECTED_STATUS]]);

  private readonly _signalRStatus: ConnectionStatus = { ...SignalRService.DISCONNECTED_STATUS };
  private _signalRRetryCount = 0;
  private readonly _hubConnection: signalR.HubConnection;

  public readonly priceUpdate$: Observable<PriceUpdate> = this._priceUpdate$.asObservable();
  public readonly backtestProgress$: Observable<BacktestProgress> = this._backtestProgress$.asObservable();
  public readonly connectionStatus$: Observable<ConnectionStatus> = this._connectionStatus$.asObservable();
  public readonly transportConnectionStatus$: Observable<ConnectionStatus> = this._transportConnectionStatus$.asObservable();

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
    this._backtestProgress$.complete();
    this._connectionStatus$.complete();
    this._transportConnectionStatus$.complete();
  }

  private _registerHandlers(): void {
    this._hubConnection.on("ReceivePriceUpdate", (update: PriceUpdate) => {
      this._priceUpdate$.next(update);
    });

    this._hubConnection.on("ReceiveBacktestProgress", (progress: BacktestProgress) => {
      this._backtestProgress$.next(progress);
    });

    this._hubConnection.on("ReceiveConnectionStatus", (status: ConnectionStatus) => {
      this._setSourceStatus(status);
    });

    this._hubConnection.on("ReceiveFillEvent", (fill: FillEvent) => {
      this._accountState.addFillEvent(fill);
    });

    this._hubConnection.on("ReceiveOrderUpdate", (orderUpdate: OrderUpdate) => {
      this._accountState.addOrderUpdateEvent(orderUpdate);
    });

    this._hubConnection.on("ReceiveUserConnectionStatus", (status: ConnectionStatus) => {
      this._setSourceStatus(status);
    });

    this._hubConnection.onreconnecting((error?: Error) => {
      this._signalRRetryCount += 1;
      this._signalRStatus.status = "Reconnecting";
      this._signalRStatus.detail = error?.message ?? null;
      this._signalRStatus.retryCount = this._signalRRetryCount;
      this._publishSignalRTransportStatus();
    });

    this._hubConnection.onreconnected(() => {
      this._signalRRetryCount = 0;
      this._signalRStatus.status = "Connected";
      this._signalRStatus.detail = null;
      this._signalRStatus.retryCount = 0;
      this._publishSignalRTransportStatus();
    });

    this._hubConnection.onclose((error?: Error) => {
      this._signalRStatus.status = "Disconnected";
      this._signalRStatus.detail = error?.message ?? null;
      this._signalRStatus.retryCount = this._signalRRetryCount;
      this._publishSignalRTransportStatus();
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
        this._publishSignalRTransportStatus();
        return;
      } catch (error: unknown) {
        this._signalRRetryCount += 1;
        this._signalRStatus.status = "Reconnecting";
        this._signalRStatus.detail = error instanceof Error ? error.message : "Unknown SignalR connection error";
        this._signalRStatus.retryCount = this._signalRRetryCount;
        this._publishSignalRTransportStatus();
      }
    }
    this._signalRStatus.status = "Disconnected";
    this._signalRStatus.detail = "Initial connection failed after all retry attempts";
    this._signalRStatus.retryCount = this._signalRRetryCount;
    this._publishSignalRTransportStatus();
  }

  private _publishSignalRTransportStatus(): void {
    const status = { ...this._signalRStatus };

    this._transportConnectionStatus$.next(status);
    this._statusBySource.set(status.source, status);
    this._publishAggregatedStatus();
  }

  private _setSourceStatus(status: ConnectionStatus): void {
    this._statusBySource.set(status.source, { ...status });
    this._publishAggregatedStatus();
  }

  private _publishAggregatedStatus(): void {
    const aggregatedStatus = Array.from(this._statusBySource.values()).reduce((current, candidate) => {
      return this._statusSeverity(candidate.status) > this._statusSeverity(current.status)
        ? candidate
        : current;
    }, SignalRService.DISCONNECTED_STATUS);

    this._connectionStatus$.next({ ...aggregatedStatus });
  }

  private _statusSeverity(status: ConnectionStatus["status"]): number {
    switch (status) {
      case "Disconnected":
        return 3;
      case "Reconnecting":
        return 2;
      case "Connected":
      default:
        return 1;
    }
  }
}
