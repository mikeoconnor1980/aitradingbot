import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatChipsModule } from "@angular/material/chips";
import { MatTableModule } from "@angular/material/table";
import { MatExpansionModule } from "@angular/material/expansion";
import { interval, of, switchMap, catchError, startWith } from "rxjs";
import { LiveTradingService } from "../../../core/services/live-trading.service";
import { GridCycle, LiveOrder } from "../../../core/models/live-trading.model";

@Component({
  selector: "app-grid-state",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatChipsModule,
    MatTableModule,
    MatExpansionModule,
  ],
  templateUrl: "./grid-state.component.html",
  styleUrls: ["./grid-state.component.scss"],
})
export class GridStateComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _liveTradingService = inject(LiveTradingService);
  private _expandedCycleId: string | null = null;

  public activeCycle: GridCycle | null = null;
  public recentCycles: GridCycle[] = [];
  public cycleOrders: LiveOrder[] = [];
  public symbol = "BTC";
  public isLoading = true;

  public readonly orderColumns = ["level", "side", "price", "size", "status", "placedAt"];

  public ngOnInit(): void {
    interval(10_000)
      .pipe(
        startWith(0),
        switchMap(() =>
          this._liveTradingService.getGridCycles(this.symbol).pipe(
            catchError(() => of([]))
          )
        ),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((cycles) => {
        this.isLoading = false;
        this.activeCycle = cycles.find((c) => c.lifecycle !== "Closed" && c.lifecycle !== "Inactive") ?? null;
        this.recentCycles = cycles.filter((c) => c.lifecycle === "Closed").slice(0, 5);
      });
  }

  public get fillProgress(): number {
    if (!this.activeCycle || this.activeCycle.totalLevels === 0) {
      return 0;
    }
    return (this.activeCycle.filledLevels / this.activeCycle.totalLevels) * 100;
  }

  public lifecycleColor(lifecycle: string): string {
    switch (lifecycle) {
      case "Active":
      case "Deploying":
        return "primary";
      case "PartiallyFilled":
        return "accent";
      case "FullyFilled":
      case "Closing":
        return "warn";
      case "Closed":
        return "";
      default:
        return "";
    }
  }

  public toggleCycleOrders(gridCycleId: string): void {
    if (this._expandedCycleId === gridCycleId) {
      this._expandedCycleId = null;
      this.cycleOrders = [];
      return;
    }

    this._expandedCycleId = gridCycleId;
    this._liveTradingService
      .getOrdersForCycle(gridCycleId)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((orders) => {
        this.cycleOrders = orders;
      });
  }

  public isCycleExpanded(gridCycleId: string): boolean {
    return this._expandedCycleId === gridCycleId;
  }

  public formatPnl(pnl: number | null): string {
    if (pnl === null) {
      return "—";
    }
    const sign = pnl >= 0 ? "+" : "";
    return `${sign}${pnl.toFixed(2)}`;
  }
}
