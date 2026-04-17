import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, catchError, map, of, shareReplay, tap } from "rxjs";
import { environment } from "../../../environments/environment";
import { ExecutionLogEntry } from "../models/execution-log.model";
import { InstallerInfo } from "../models/installer-info.model";
import { PlaceOrderRequest, PlaceOrderResponse } from "../models/place-order.model";
import { PlaceTriggerOrderRequest, PlaceTriggerOrderResponse } from "../models/trigger-order.model";

export interface AgentInfo {
  agentId: string;
  machineName: string;
  state: AgentState;
  lastHeartbeat: string;
  connectedSince: string;
  walletAddress: string | null;
  activeStrategy: ActiveStrategyInfo | null;
  lastError: string | null;
  killedAtUtc: string | null;
  killedReason: string | null;
}

export interface ActiveStrategyInfo {
  strategyName: string;
  market: string;
  timeframe: string;
  startedAtUtc: string;
}

export type AgentState = "idle" | "starting" | "running" | "stopping" | "error" | "disconnected" | "killed";

export interface CommandAcceptedResponse {
  commandId: string;
}

export interface PendingCommand {
  commandId: string;
  type: string;
  createdAtUtc: string;
}

@Injectable({ providedIn: "root" })
export class AgentService {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = environment.apiBaseUrl;

  private readonly _agents$ = new BehaviorSubject<AgentInfo[]>([]);
  public readonly agents$: Observable<AgentInfo[]> = this._agents$.asObservable();

  /** The currently selected agent for order routing. */
  private readonly _selectedAgentId$ = new BehaviorSubject<string | null>(null);
  public readonly selectedAgentId$: Observable<string | null> = this._selectedAgentId$.asObservable();

  public get selectedAgentId(): string | null {
    return this._selectedAgentId$.value;
  }

  public selectAgent(agentId: string | null): void {
    this._selectedAgentId$.next(agentId);
  }

  public refreshAgents(): void {
    this._http
      .get<AgentInfo[]>(`${this._baseUrl}/agent/list`)
      .pipe(
        catchError(() => of<AgentInfo[]>([]))
      )
      .subscribe((agents) => {
        this._agents$.next(agents);

        // Auto-select first connected agent if none selected
        if (!this._selectedAgentId$.value) {
          const connected = agents.find(a => a.state !== "disconnected");
          if (connected) {
            this._selectedAgentId$.next(connected.agentId);
          }
        }
      });
  }

  public getAgent(agentId: string): Observable<AgentInfo> {
    return this._http.get<AgentInfo>(`${this._baseUrl}/agent/${agentId}`);
  }

  public startTrading(agentId: string, strategyConfig: unknown): Observable<CommandAcceptedResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`${this._baseUrl}/trading/${agentId}/start`, { strategyConfig })
      .pipe(
        tap(() => {
          setTimeout(() => this.refreshAgents(), 1000);
        })
      );
  }

  public stopTrading(agentId: string): Observable<CommandAcceptedResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`${this._baseUrl}/trading/${agentId}/stop`, {})
      .pipe(
        tap(() => {
          setTimeout(() => this.refreshAgents(), 1000);
        })
      );
  }

  /**
   * Route an order through an agent. Returns a PlaceOrderResponse-compatible observable.
   * The order is queued and executed by the agent on its next heartbeat.
   */
  public placeOrderViaAgent(agentId: string, request: PlaceOrderRequest): Observable<PlaceOrderResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`${this._baseUrl}/trading/${agentId}/order`, request)
      .pipe(
        map((resp) => ({
          success: true,
          orderId: null,
          status: "queued",
          detail: `Order queued for agent (command: ${resp.commandId}). Will execute on next check-in.`
        })),
        catchError((err) => of({
          success: false,
          orderId: null,
          status: "error",
          detail: err?.error?.detail ?? err?.message ?? "Failed to queue order."
        }))
      );
  }

  public cancelOrderViaAgent(agentId: string, orderId: string, asset: string): Observable<CommandAcceptedResponse> {
    return this._http.post<CommandAcceptedResponse>(
      `${this._baseUrl}/trading/${agentId}/cancel-order`,
      { orderId, asset }
    );
  }

  public cancelAllOrdersViaAgent(agentId: string, asset: string): Observable<CommandAcceptedResponse> {
    return this._http.post<CommandAcceptedResponse>(
      `${this._baseUrl}/trading/${agentId}/cancel-all-orders`,
      { asset }
    );
  }

  public placeTriggerOrderViaAgent(agentId: string, request: PlaceTriggerOrderRequest): Observable<PlaceTriggerOrderResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`${this._baseUrl}/trading/${agentId}/trigger-order`, request)
      .pipe(
        map((resp) => ({
          success: true,
          orderId: null,
          status: "queued",
          detail: `Trigger order queued for agent (command: ${resp.commandId}).`
        })),
        catchError((err) => of({
          success: false,
          orderId: null,
          status: "error",
          detail: err?.error?.detail ?? err?.message ?? "Failed to queue trigger order."
        }))
      );
  }

  public modifyTriggerOrderViaAgent(
    agentId: string,
    orderId: string,
    asset: string,
    side: string,
    triggerPrice: number,
    size: number,
    tpslType: string
  ): Observable<CommandAcceptedResponse> {
    return this._http.post<CommandAcceptedResponse>(
      `${this._baseUrl}/trading/${agentId}/modify-trigger-order`,
      { orderId, asset, side, triggerPrice, size, tpslType }
    );
  }

  public cancelTriggerOrderViaAgent(agentId: string, orderId: string, asset: string): Observable<CommandAcceptedResponse> {
    return this._http.post<CommandAcceptedResponse>(
      `${this._baseUrl}/trading/${agentId}/cancel-trigger-order`,
      { orderId, asset }
    );
  }

  public getPendingCommands(agentId: string): Observable<PendingCommand[]> {
    return this._http.get<PendingCommand[]>(`${this._baseUrl}/agent/${agentId}/pending-commands`).pipe(
      catchError(() => of<PendingCommand[]>([]))
    );
  }

  public killAgent(agentId: string, reason?: string, effectiveAtUtc?: string): Observable<unknown> {
    return this._http
      .post(`${this._baseUrl}/agent/${agentId}/kill`, { reason, effectiveAtUtc })
      .pipe(tap(() => setTimeout(() => this.refreshAgents(), 500)));
  }

  public reinstateAgent(agentId: string): Observable<unknown> {
    return this._http
      .post(`${this._baseUrl}/agent/${agentId}/reinstate`, {})
      .pipe(tap(() => setTimeout(() => this.refreshAgents(), 500)));
  }

  public getExecutionLogs(agentId: string, since?: string, limit?: number, level?: string): Observable<ExecutionLogEntry[]> {
    let params = new HttpParams();
    if (since) params = params.set("since", since);
    if (limit) params = params.set("limit", limit.toString());
    if (level) params = params.set("level", level);

    return this._http
      .get<ExecutionLogEntry[]>(`${this._baseUrl}/agent/${agentId}/execution-logs`, { params })
      .pipe(catchError(() => of<ExecutionLogEntry[]>([])));
  }

  private _installerInfo$: Observable<InstallerInfo> | null = null;

  public getInstallerInfo(): Observable<InstallerInfo> {
    if (!this._installerInfo$) {
      this._installerInfo$ = this._http
        .get<InstallerInfo>(`${this._baseUrl}/agent/installer/info`)
        .pipe(shareReplay({ bufferSize: 1, refCount: true }));
    }
    return this._installerInfo$;
  }

  public getInstallerDownloadUrl(format: "exe" | "zip"): string {
    return `${this._baseUrl}/agent/installer/download?format=${format}`;
  }
}
