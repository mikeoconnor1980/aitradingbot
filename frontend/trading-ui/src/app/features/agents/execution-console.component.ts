import { Component, DestroyRef, inject, Input, OnChanges, signal, SimpleChanges, ViewChild, ElementRef, AfterViewChecked } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { ExecutionLogEntry } from "../../core/models/execution-log.model";
import { AgentService } from "../../core/services/agent.service";
import { SignalRService } from "../../core/services/signalr.service";
import { filter } from "rxjs";

@Component({
  selector: "app-execution-console",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatButtonToggleModule,
  ],
  templateUrl: "./execution-console.component.html",
  styleUrl: "./execution-console.component.scss"
})
export class ExecutionConsoleComponent implements OnChanges, AfterViewChecked {
  private readonly _agentService = inject(AgentService);
  private readonly _signalR = inject(SignalRService);
  private readonly _destroyRef = inject(DestroyRef);

  @ViewChild("scrollContainer") private _scrollContainer!: ElementRef<HTMLDivElement>;

  @Input() public agentId: string | null = null;

  public readonly entries = signal<ExecutionLogEntry[]>([]);
  public readonly level = signal<string>("Summary");
  public readonly autoScroll = signal<boolean>(true);
  public readonly paused = signal<boolean>(false);
  public readonly expanded = signal<boolean>(false);

  private _shouldScroll = false;
  private static readonly MAX_ENTRIES = 500;

  public constructor() {
    this._signalR.executionLog$
      .pipe(
        filter((entry) => !this.paused() && (this.agentId === null || entry.agentId === this.agentId)),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((entry) => {
        this._appendEntry(entry);
      });
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["agentId"] && this.agentId) {
      this._loadHistory();
    }
  }

  public ngAfterViewChecked(): void {
    if (this._shouldScroll && this.autoScroll() && this._scrollContainer) {
      const el = this._scrollContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
      this._shouldScroll = false;
    }
  }

  public onToggleLevel(value: string): void {
    this.level.set(value);
    if (this.agentId) {
      this._loadHistory();
    }
  }

  public onClear(): void {
    this.entries.set([]);
  }

  public onTogglePause(): void {
    this.paused.update((v) => !v);
  }

  public onToggleExpand(): void {
    this.expanded.update((v) => !v);
  }

  public getCategoryIcon(category: string): string {
    switch (category) {
      case "CandleClose": return "candlestick_chart";
      case "EntryGate": return "login";
      case "ExitCheck": return "logout";
      case "RiskEngine": return "shield";
      case "Signal": return "bolt";
      case "GridState": return "grid_on";
      case "Drawdown": return "trending_down";
      case "Indicator": return "insights";
      default: return "info";
    }
  }

  public getCategoryClass(category: string): string {
    switch (category) {
      case "RiskEngine": return "risk";
      case "Signal": return "signal";
      case "Drawdown": return "drawdown";
      case "EntryGate":
      case "ExitCheck": return "gate";
      default: return "default";
    }
  }

  public formatTimestamp(isoDate: string): string {
    const d = new Date(isoDate);
    return d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
  }

  public formatData(data: Record<string, unknown> | null): string {
    if (!data) return "";
    return Object.entries(data)
      .map(([k, v]) => `${k}=${v}`)
      .join(" · ");
  }

  private _loadHistory(): void {
    if (!this.agentId) return;

    // Summary mode filters to Summary only; Detail mode shows all entries
    const levelFilter = this.level() === "Summary" ? "Summary" : undefined;

    this._agentService.getExecutionLogs(this.agentId, undefined, 200, levelFilter)
      .subscribe((logs) => {
        // API returns newest-first; reverse for chronological display
        this.entries.set(logs.reverse());
        this._shouldScroll = true;
      });
  }

  private _appendEntry(entry: ExecutionLogEntry): void {
    const current = this.entries();
    const updated = current.length >= ExecutionConsoleComponent.MAX_ENTRIES
      ? [...current.slice(current.length - ExecutionConsoleComponent.MAX_ENTRIES + 1), entry]
      : [...current, entry];
    this.entries.set(updated);
    this._shouldScroll = true;
  }
}
