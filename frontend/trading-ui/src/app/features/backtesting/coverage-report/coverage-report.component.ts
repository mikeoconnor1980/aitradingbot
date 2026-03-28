import { DatePipe, DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { CoverageReport } from "../../../core/models/backtest.model";

type CoverageStatus = "full" | "partial" | "none";

interface CoverageRow {
  key: string;
  symbol: string;
  interval: string;
  from: string | null;
  to: string | null;
  candleCount: number;
  status: CoverageStatus;
}

@Component({
  selector: "app-coverage-report",
  standalone: true,
  imports: [MatCardModule, MatIconModule, DecimalPipe, DatePipe],
  templateUrl: "./coverage-report.component.html",
  styleUrl: "./coverage-report.component.scss"
})
export class CoverageReportComponent {
  @Input()
  public report: CoverageReport | null = null;

  public get rows(): CoverageRow[] {
    if (this.report === null) {
      return [];
    }

    const intervalOrder = ["15m", "1h", "4h"];

    return Object.entries(this.report.coverage)
      .map(([key, coverage]) => {
        const [symbol, interval] = key.split("/");
        const hasFrom = coverage.from !== null;
        const hasTo = coverage.to !== null;
        let status: CoverageStatus = "none";

        if (coverage.candleCount > 0 && hasFrom && hasTo) {
          status = "full";
        } else if (coverage.candleCount > 0 || hasFrom || hasTo) {
          status = "partial";
        }

        return {
          key,
          symbol,
          interval: interval ?? key,
          from: coverage.from,
          to: coverage.to,
          candleCount: coverage.candleCount,
          status
        };
      })
      .sort((left, right) => {
        const leftIndex = intervalOrder.indexOf(left.interval);
        const rightIndex = intervalOrder.indexOf(right.interval);

        if (leftIndex === -1 || rightIndex === -1) {
          return left.interval.localeCompare(right.interval);
        }

        return leftIndex - rightIndex;
      });
  }

  public get heading(): string {
    const firstRow = this.rows[0];
    return firstRow === undefined ? "Data Coverage" : `Data Coverage - ${firstRow.symbol}`;
  }

  public getStatusClass(status: CoverageStatus): string {
    return `coverage-report__status coverage-report__status--${status}`;
  }

  public getStatusIcon(status: CoverageStatus): string {
    switch (status) {
      case "full":
        return "check_circle";
      case "partial":
        return "warning";
      default:
        return "cancel";
    }
  }

  public getStatusLabel(status: CoverageStatus): string {
    switch (status) {
      case "full":
        return "Full";
      case "partial":
        return "Partial";
      default:
        return "None";
    }
  }
}