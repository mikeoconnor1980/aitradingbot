import { Component, Input, OnChanges, SimpleChanges } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import {
  BacktestDebugResponse,
  GridCycleSummary,
  OrderEvent,
  OrderEventType
} from "../../../core/models/backtest-debug.model";

@Component({
  selector: "app-cycle-narrative",
  standalone: true,
  imports: [MatIconModule],
  templateUrl: "./cycle-narrative.component.html",
  styleUrl: "./cycle-narrative.component.scss"
})
export class CycleNarrativeComponent implements OnChanges {
  @Input()
  public debugData: BacktestDebugResponse | null = null;

  @Input()
  public symbol = "";

  public narrativeLines: NarrativeLine[] = [];
  public exitIcon = "";
  public exitClass = "";

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["debugData"]) {
      this._buildNarrative();
    }
  }

  private _buildNarrative(): void {
    this.narrativeLines = [];
    this.exitIcon = "";
    this.exitClass = "";

    const summary = this.debugData?.gridCycleSummary;
    if (!summary) {
      this.narrativeLines = [{
        icon: "info",
        text: "No cycle summary available.",
        cssClass: "narrative__line--muted"
      }];
      return;
    }

    const events = this.debugData?.orderEvents ?? [];
    const lines: NarrativeLine[] = [];

    // 1. Deployment
    lines.push({
      icon: "rocket_launch",
      text: this._buildDeploymentLine(summary),
      cssClass: "narrative__line--deploy"
    });

    // 2. Grid structure
    lines.push({
      icon: "grid_on",
      text: this._buildGridStructureLine(summary),
      cssClass: "narrative__line--structure"
    });

    // 3. Fills
    const fillLines = this._buildFillLines(summary, events);
    for (const line of fillLines) {
      lines.push(line);
    }

    // 4. Exit
    const exitLine = this._buildExitLine(summary);
    lines.push(exitLine);
    this.exitIcon = exitLine.icon;
    this.exitClass = exitLine.cssClass;

    // 5. Result
    lines.push({
      icon: summary.cyclePnl >= 0 ? "trending_up" : "trending_down",
      text: this._buildResultLine(summary),
      cssClass: summary.cyclePnl >= 0 ? "narrative__line--profit" : "narrative__line--loss"
    });

    this.narrativeLines = lines;
  }

  private _buildDeploymentLine(summary: GridCycleSummary): string {
    const deployDate = this._formatTimestamp(summary.deployTimestampUtc);
    const anchor = this._formatPrice(summary.anchorPrice);
    return `Grid deployed on ${deployDate} with anchor price at ${anchor}.`;
  }

  private _buildGridStructureLine(summary: GridCycleSummary): string {
    const levels = summary.levelsPlaced;
    const lowest = this._formatPrice(Math.min(...summary.levelPrices));
    const highest = this._formatPrice(Math.max(...summary.levelPrices));
    const tp = summary.takeProfitPrice > 0 ? ` Take profit set at ${this._formatPrice(summary.takeProfitPrice)}.` : "";
    const sl = summary.stopLossPrice && summary.stopLossPrice > 0
      ? ` Stop loss at ${this._formatPrice(summary.stopLossPrice)}.`
      : "";

    return `${levels} buy levels placed from ${lowest} to ${highest}.${tp}${sl}`;
  }

  private _buildFillLines(summary: GridCycleSummary, events: OrderEvent[]): NarrativeLine[] {
    const fills = events.filter((e: OrderEvent) => e.eventType === OrderEventType.Filled && e.side === "Buy");

    if (fills.length === 0) {
      return [{
        icon: "hourglass_empty",
        text: "No grid levels were filled before exit.",
        cssClass: "narrative__line--muted"
      }];
    }

    if (fills.length === 1) {
      const fill = fills[0];
      const fillPrice = this._formatPrice(fill.fillPrice ?? fill.price);
      const fillDate = this._formatTimestamp(fill.timestampUtc);
      return [{
        icon: "shopping_cart",
        text: `Price dropped to ${fillPrice} on ${fillDate}, filling level 1 of ${summary.levelsPlaced}.`,
        cssClass: "narrative__line--fill"
      }];
    }

    const firstFill = fills[0];
    const lastFill = fills[fills.length - 1];
    const lowestFill = this._formatPrice(Math.min(...fills.map((f: OrderEvent) => f.fillPrice ?? f.price)));
    const firstDate = this._formatTimestamp(firstFill.timestampUtc);
    const lastDate = this._formatTimestamp(lastFill.timestampUtc);

    const lines: NarrativeLine[] = [{
      icon: "shopping_cart",
      text: `${fills.length} of ${summary.levelsPlaced} levels filled between ${firstDate} and ${lastDate}, buying down to ${lowestFill}.`,
      cssClass: "narrative__line--fill"
    }];

    return lines;
  }

  private _buildExitLine(summary: GridCycleSummary): NarrativeLine {
    const closeDate = this._formatTimestamp(summary.closeTimestampUtc);
    const duration = this._formatDuration(summary.cycleDurationMs);
    const reason = summary.exitReason;

    switch (reason) {
      case "TakeProfit":
        return {
          icon: "emoji_events",
          text: `Take profit hit on ${closeDate} after ${duration}. Price reached ${this._formatPrice(summary.takeProfitPrice)}.`,
          cssClass: "narrative__line--profit"
        };
      case "StopLoss":
        return {
          icon: "shield",
          text: `Stop loss triggered on ${closeDate} after ${duration}.`,
          cssClass: "narrative__line--loss"
        };
      case "Breakdown":
        return {
          icon: "trending_down",
          text: `Grid broke down on ${closeDate} after ${duration}. Price fell below breakdown threshold.`,
          cssClass: "narrative__line--loss"
        };
      default:
        return {
          icon: "logout",
          text: `Grid closed on ${closeDate} after ${duration}. Reason: ${reason}.`,
          cssClass: "narrative__line--muted"
        };
    }
  }

  private _buildResultLine(summary: GridCycleSummary): string {
    const pnl = summary.cyclePnl;
    const pnlStr = pnl >= 0 ? `+$${pnl.toFixed(2)}` : `-$${Math.abs(pnl).toFixed(2)}`;
    return `Cycle result: ${pnlStr} PnL with ${summary.levelsFilled} of ${summary.levelsPlaced} levels filled.`;
  }

  private _formatPrice(price: number): string {
    return `$${price.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  private _formatTimestamp(timestampMs: number): string {
    const date = new Date(timestampMs);
    return date.toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric",
      hour: "numeric",
      minute: "2-digit",
      hour12: true
    });
  }

  private _formatDuration(totalMs: number): string {
    const totalMinutes = Math.floor(totalMs / 60000);

    if (totalMinutes < 60) {
      return `${totalMinutes}m`;
    }

    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours < 24) {
      return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
    }

    const days = Math.floor(hours / 24);
    const remainingHours = hours % 24;
    const parts = [`${days}d`];
    if (remainingHours > 0) {
      parts.push(`${remainingHours}h`);
    }
    return parts.join(" ");
  }
}

export interface NarrativeLine {
  icon: string;
  text: string;
  cssClass: string;
}
