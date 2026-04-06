import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { interval } from "rxjs";
import { AgentInfo, AgentService, AgentState } from "../../core/services/agent.service";
import { StartTradingDialogComponent, StartTradingDialogResult } from "./start-trading-dialog.component";

@Component({
  selector: "app-agents-page",
  standalone: true,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
  ],
  templateUrl: "./agents-page.component.html",
  styleUrl: "./agents-page.component.scss"
})
export class AgentsPageComponent implements OnInit {
  private readonly _agentService = inject(AgentService);
  private readonly _dialog = inject(MatDialog);
  private readonly _destroyRef = inject(DestroyRef);

  public readonly agents = signal<AgentInfo[]>([]);
  public readonly displayedColumns = ["status", "agentId", "machineName", "wallet", "strategy", "lastHeartbeat", "actions"];

  public ngOnInit(): void {
    this._agentService.refreshAgents();

    this._agentService.agents$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((agents) => this.agents.set(agents));

    // Auto-refresh every 5 seconds
    interval(5000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._agentService.refreshAgents());
  }

  public getStateIcon(state: AgentState): string {
    switch (state) {
      case "Running": return "play_circle";
      case "Idle": return "pause_circle";
      case "Starting": return "hourglass_top";
      case "Stopping": return "hourglass_bottom";
      case "Error": return "error";
      case "Disconnected": return "cloud_off";
      default: return "help";
    }
  }

  public getStateColor(state: AgentState): string {
    switch (state) {
      case "Running": return "primary";
      case "Idle": return "";
      case "Starting":
      case "Stopping": return "accent";
      case "Error":
      case "Disconnected": return "warn";
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
    return agent.state === "Idle" || agent.state === "Error";
  }

  public canStop(agent: AgentInfo): boolean {
    return agent.state === "Running" || agent.state === "Starting";
  }
}
