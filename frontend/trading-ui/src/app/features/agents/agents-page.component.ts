import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { interval, forkJoin, of, switchMap } from "rxjs";
import { AgentInfo, AgentService, AgentState, PendingCommand } from "../../core/services/agent.service";
import { StartTradingDialogComponent, StartTradingDialogResult } from "./start-trading-dialog.component";
import { KillSwitchDialogComponent, KillSwitchDialogResult } from "./kill-switch-dialog.component";
import { ExecutionConsoleComponent } from "./execution-console.component";

@Component({
  selector: "app-agents-page",
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
    ExecutionConsoleComponent,
  ],
  templateUrl: "./agents-page.component.html",
  styleUrl: "./agents-page.component.scss"
})
export class AgentsPageComponent implements OnInit {
  private readonly _agentService = inject(AgentService);
  private readonly _dialog = inject(MatDialog);
  private readonly _destroyRef = inject(DestroyRef);

  public readonly agents = signal<AgentInfo[]>([]);
  public readonly pendingCommands = signal<Record<string, PendingCommand[]>>({});
  public readonly selectedAgentId = signal<string | null>(null);
  public readonly displayedColumns = ["status", "agentId", "machineName", "wallet", "strategy", "lastHeartbeat", "queue", "actions"];

  public ngOnInit(): void {
    this._agentService.refreshAgents();

    this._agentService.agents$
      .pipe(
        switchMap((agents) => {
          this.agents.set(agents);
          if (agents.length === 0) {
            return of({} as Record<string, PendingCommand[]>);
          }
          const requests: Record<string, ReturnType<AgentService["getPendingCommands"]>> = {};
          for (const agent of agents) {
            requests[agent.agentId] = this._agentService.getPendingCommands(agent.agentId);
          }
          return forkJoin(requests);
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((result) => {
        this.pendingCommands.set(result);
      });

    // Auto-refresh every 5 seconds
    interval(5000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._agentService.refreshAgents());
  }

  public getStateIcon(state: AgentState): string {
    switch (state) {
      case "running": return "play_circle";
      case "idle": return "pause_circle";
      case "starting": return "hourglass_top";
      case "stopping": return "hourglass_bottom";
      case "error": return "error";
      case "disconnected": return "cloud_off";
      case "killed": return "block";
      default: return "help";
    }
  }

  public getStateColor(state: AgentState): string {
    switch (state) {
      case "running": return "primary";
      case "idle": return "";
      case "starting":
      case "stopping": return "accent";
      case "error":
      case "disconnected": return "warn";
      case "killed": return "warn";
      default: return "";
    }
  }

  public getTimeSince(isoDate: string): string {
    const diff = Date.now() - new Date(isoDate).getTime();
    const seconds = Math.floor(diff / 1000);
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }

  public formatDuration(isoDate: string): string {
    const diff = Date.now() - new Date(isoDate).getTime();
    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    if (hours > 0) return `${hours}h ${minutes}m`;
    return `${minutes}m`;
  }

  public onStartTrading(agent: AgentInfo): void {
    const dialogRef = this._dialog.open(StartTradingDialogComponent, {
      width: "500px",
      data: { agentId: agent.agentId }
    });

    dialogRef.afterClosed().subscribe((result: StartTradingDialogResult | undefined) => {
      if (result) {
        this._agentService.startTrading(agent.agentId, result.strategyConfig).subscribe({
          error: (err) => console.error("Failed to start trading:", err)
        });
      }
    });
  }

  public onStopTrading(agent: AgentInfo): void {
    this._agentService.stopTrading(agent.agentId).subscribe({
      error: (err) => console.error("Failed to stop trading:", err)
    });
  }

  public canStart(agent: AgentInfo): boolean {
    return agent.state === "idle" || agent.state === "error";
  }

  public canStop(agent: AgentInfo): boolean {
    return agent.state === "running" || agent.state === "starting";
  }

  public canKill(agent: AgentInfo): boolean {
    return agent.state !== "killed";
  }

  public isKilled(agent: AgentInfo): boolean {
    return agent.state === "killed";
  }

  public isScheduledKill(agent: AgentInfo): boolean {
    if (!agent.killedAtUtc) return false;
    return new Date(agent.killedAtUtc).getTime() > Date.now();
  }

  public onKillAgent(agent: AgentInfo): void {
    const dialogRef = this._dialog.open(KillSwitchDialogComponent, {
      width: "450px",
      data: { agentId: agent.agentId }
    });

    dialogRef.afterClosed().subscribe((result: KillSwitchDialogResult | undefined) => {
      if (result) {
        this._agentService.killAgent(agent.agentId, result.reason, result.effectiveAtUtc).subscribe({
          error: (err) => console.error("Failed to kill agent:", err)
        });
      }
    });
  }

  public onReinstateAgent(agent: AgentInfo): void {
    this._agentService.reinstateAgent(agent.agentId).subscribe({
      error: (err) => console.error("Failed to reinstate agent:", err)
    });
  }

  public getQueuedCommands(agentId: string): PendingCommand[] {
    return this.pendingCommands()[agentId] ?? [];
  }

  public formatCommandType(type: string): string {
    return type.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase());
  }

  public onSelectAgent(agent: AgentInfo): void {
    this.selectedAgentId.set(agent.agentId);
  }
}
