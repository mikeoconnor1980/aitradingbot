import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, catchError, of, tap } from "rxjs";

export interface AgentInfo {
  agentId: string;
  machineName: string;
  state: AgentState;
  lastHeartbeat: string;
  connectedSince: string;
  walletAddress: string | null;
  activeStrategy: ActiveStrategyInfo | null;
  lastError: string | null;
}

export interface ActiveStrategyInfo {
  strategyName: string;
  market: string;
  timeframe: string;
  startedAtUtc: string;
}

export type AgentState = "Idle" | "Starting" | "Running" | "Stopping" | "Error" | "Disconnected";

export interface StrategyConfig {
  strategyName: string;
  strategyMode: string;
  exchange: string;
  market: string;
  timeframe: string;
  direction: string;
  enabled: boolean;
  grid?: {
    levels: number;
    spacingPercent: number;
    notionalPerLevel: number;
  };
  risk?: {
    maxPositionSize: number;
    maxDrawdownPercent: number;
  };
}

export interface CommandAcceptedResponse {
  commandId: string;
}

@Injectable({ providedIn: "root" })
export class AgentService {
  private readonly _http = inject(HttpClient);

  private readonly _agents$ = new BehaviorSubject<AgentInfo[]>([]);
  public readonly agents$: Observable<AgentInfo[]> = this._agents$.asObservable();

  public refreshAgents(): void {
    this._http
      .get<AgentInfo[]>("/api/agent/list")
      .pipe(
        catchError(() => of<AgentInfo[]>([]))
      )
      .subscribe((agents) => this._agents$.next(agents));
  }

  public getAgent(agentId: string): Observable<AgentInfo> {
    return this._http.get<AgentInfo>(`/api/agent/${agentId}`);
  }

  public startTrading(agentId: string, strategyConfig: StrategyConfig): Observable<CommandAcceptedResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`/api/trading/${agentId}/start`, { strategyConfig })
      .pipe(
        tap(() => {
          // Refresh agents after command sent
          setTimeout(() => this.refreshAgents(), 1000);
        })
      );
  }

  public stopTrading(agentId: string): Observable<CommandAcceptedResponse> {
    return this._http
      .post<CommandAcceptedResponse>(`/api/trading/${agentId}/stop`, {})
      .pipe(
        tap(() => {
          setTimeout(() => this.refreshAgents(), 1000);
        })
      );
  }
}
