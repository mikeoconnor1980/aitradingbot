import { Component, Input, OnChanges, SimpleChanges } from "@angular/core";
import { MatCardModule } from "@angular/material/card";
import { MatTableModule } from "@angular/material/table";
import { BacktestResult } from "../../../core/models/backtest.model";
import { EquityChartComponent } from "../equity-chart/equity-chart.component";

interface ComparisonRow {
  metric: string;
  valueA: string;
  valueB: string;
  delta: string;
  deltaClass: string;
}

interface ConfigDiffItem {
  label: string;
  valueA: string;
  valueB: string;
  changed: boolean;
}

type DeltaPreference = "higher" | "lower" | "closer-to-zero" | "neutral";

@Component({
  selector: "app-backtest-compare",
  standalone: true,
  imports: [MatCardModule, MatTableModule, EquityChartComponent],
  templateUrl: "./backtest-compare.component.html",
  styleUrl: "./backtest-compare.component.scss"
})
export class BacktestCompareComponent implements OnChanges {
  @Input({ required: true })
  public resultA!: BacktestResult;

  @Input({ required: true })
  public resultB!: BacktestResult;

  public readonly displayedColumns = ["metric", "valueA", "valueB", "delta"];
  public comparisonRows: ComparisonRow[] = [];
  public configDiffs: ConfigDiffItem[] = [];
  public runALabel = "Run A";
  public runBLabel = "Run B";
  public runADetail = "";
  public runBDetail = "";

  public ngOnChanges(changes: SimpleChanges): void {
    if ((changes["resultA"] || changes["resultB"]) && this.resultA && this.resultB) {
      this._buildComparisonRows();
      this._buildConfigDiffs();
      this._buildRunLabels();
    }
  }

  private _buildComparisonRows(): void {
    this.comparisonRows = [
      this._numericRow("Total PnL", this.resultA.totalPnl, this.resultB.totalPnl, "$", "higher"),
      this._numericRow("Lot Win Rate", this.resultA.winRate, this.resultB.winRate, "%", "higher"),
      this._numericRow("Max Drawdown", this.resultA.maxDrawdown, this.resultB.maxDrawdown, "$", "closer-to-zero"),
      this._numericRow("Trade Lots", this.resultA.totalTrades, this.resultB.totalTrades, "", "neutral"),
      this._numericRow("Winning Lots", this.resultA.winningTrades, this.resultB.winningTrades, "", "higher"),
      this._numericRow("Losing Lots", this.resultA.losingTrades, this.resultB.losingTrades, "", "lower"),
      this._numericRow("Avg Lot PnL", this.resultA.averageTradePnl, this.resultB.averageTradePnl, "$", "higher"),
      this._durationRow("Avg Hold Time", this.resultA.averageHoldTimeMinutes, this.resultB.averageHoldTimeMinutes),
      this._numericRow("Hedges Opened", this.resultA.hedgesOpened, this.resultB.hedgesOpened, "", "neutral"),
      this._numericRow("Total Fees", this.resultA.totalFeesPaid, this.resultB.totalFeesPaid, "$", "lower")
    ];
  }

  private _buildConfigDiffs(): void {
    const metrics = [
      { label: "Symbol", valueA: this.resultA.symbol, valueB: this.resultB.symbol },
      { label: "Intervals", valueA: this.resultA.intervals.join(", "), valueB: this.resultB.intervals.join(", ") },
      { label: "Initial Capital", valueA: this._formatCurrency(this.resultA.initialCapital), valueB: this._formatCurrency(this.resultB.initialCapital) },
      { label: "Grid Levels", valueA: String(this.resultA.strategyConfig.gridLevels), valueB: String(this.resultB.strategyConfig.gridLevels) },
      { label: "Entry Mode", valueA: this._formatEntryMode(this.resultA.strategyConfig.entryMode), valueB: this._formatEntryMode(this.resultB.strategyConfig.entryMode) },
      { label: "Limit Price", valueA: this._formatLimitPrice(this.resultA.strategyConfig.entryMode, this.resultA.strategyConfig.manualAnchorPrice), valueB: this._formatLimitPrice(this.resultB.strategyConfig.entryMode, this.resultB.strategyConfig.manualAnchorPrice) },
      { label: "Grid Spacing", valueA: this._formatPercent(this.resultA.strategyConfig.gridSpacing), valueB: this._formatPercent(this.resultB.strategyConfig.gridSpacing) },
      { label: "Take Profit", valueA: this._formatPercent(this.resultA.strategyConfig.takeProfitPercent), valueB: this._formatPercent(this.resultB.strategyConfig.takeProfitPercent) },
      { label: "Breakdown Threshold", valueA: this._formatPercent(this.resultA.strategyConfig.breakdownThreshold), valueB: this._formatPercent(this.resultB.strategyConfig.breakdownThreshold) },
      { label: "Position Size", valueA: this._formatCurrency(this.resultA.strategyConfig.positionSize), valueB: this._formatCurrency(this.resultB.strategyConfig.positionSize) },
      { label: "Leverage", valueA: `${this.resultA.strategyConfig.leverage}x`, valueB: `${this.resultB.strategyConfig.leverage}x` },
      { label: "Stop Loss", valueA: this._formatPercent(this.resultA.strategyConfig.stopLossPercent), valueB: this._formatPercent(this.resultB.strategyConfig.stopLossPercent) },
      { label: "Maker Fee", valueA: this._formatRate(this.resultA.strategyConfig.makerFee), valueB: this._formatRate(this.resultB.strategyConfig.makerFee) },
      { label: "Taker Fee", valueA: this._formatRate(this.resultA.strategyConfig.takerFee), valueB: this._formatRate(this.resultB.strategyConfig.takerFee) },
      { label: "Slippage", valueA: this._formatRate(this.resultA.strategyConfig.slippage), valueB: this._formatRate(this.resultB.strategyConfig.slippage) }
    ];

    this.configDiffs = metrics.map((item) => ({
      ...item,
      changed: item.valueA !== item.valueB
    }));
  }

  private _buildRunLabels(): void {
    const firstDiff = this.configDiffs.find((item) => item.changed);

    if (firstDiff) {
      this.runALabel = `Run A - ${firstDiff.label}: ${firstDiff.valueA}`;
      this.runBLabel = `Run B - ${firstDiff.label}: ${firstDiff.valueB}`;
    }

    this.runADetail = `${this.resultA.symbol} | ${this._formatDate(this.resultA.startDate)} to ${this._formatDate(this.resultA.endDate)}`;
    this.runBDetail = `${this.resultB.symbol} | ${this._formatDate(this.resultB.startDate)} to ${this._formatDate(this.resultB.endDate)}`;
  }

  private _numericRow(
    metric: string,
    valueA: number,
    valueB: number,
    suffix: string,
    preference: DeltaPreference
  ): ComparisonRow {
    const delta = valueA - valueB;
    const deltaClass = this._getDeltaClass(valueA, valueB, preference);
    const arrow = delta > 0 ? "▲" : delta < 0 ? "▼" : "•";

    return {
      metric,
      valueA: this._formatValue(valueA, suffix),
      valueB: this._formatValue(valueB, suffix),
      delta: `${arrow} ${this._formatSignedDelta(delta, suffix)}`,
      deltaClass
    };
  }

  private _durationRow(metric: string, minutesA: number, minutesB: number): ComparisonRow {
    const delta = minutesA - minutesB;
    const arrow = delta > 0 ? "▲" : delta < 0 ? "▼" : "•";

    return {
      metric,
      valueA: this._formatDuration(minutesA),
      valueB: this._formatDuration(minutesB),
      delta: `${arrow} ${this._formatDuration(Math.abs(delta))}`,
      deltaClass: "backtest-compare__delta--neutral"
    };
  }

  private _getDeltaClass(valueA: number, valueB: number, preference: DeltaPreference): string {
    if (preference === "neutral" || valueA === valueB) {
      return "backtest-compare__delta--neutral";
    }

    if (preference === "higher") {
      return valueA > valueB ? "backtest-compare__delta--better" : "backtest-compare__delta--worse";
    }

    if (preference === "lower") {
      return valueA < valueB ? "backtest-compare__delta--better" : "backtest-compare__delta--worse";
    }

    return Math.abs(valueA) < Math.abs(valueB)
      ? "backtest-compare__delta--better"
      : "backtest-compare__delta--worse";
  }

  private _formatValue(value: number, suffix: string): string {
    if (suffix === "$") {
      return this._formatCurrency(value);
    }

    if (suffix === "%") {
      return this._formatPercent(value);
    }

    return value.toLocaleString("en-US", { maximumFractionDigits: 2 });
  }

  private _formatSignedDelta(value: number, suffix: string): string {
    if (value === 0) {
      return suffix === "$" ? "$0.00" : suffix === "%" ? "0.0%" : "0";
    }

    const absoluteValue = Math.abs(value);
    const formatted = this._formatValue(absoluteValue, suffix);
    return `${value > 0 ? "+" : "-"}${formatted}`;
  }

  private _formatCurrency(value: number): string {
    return `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  private _formatPercent(value: number): string {
    return `${value.toLocaleString("en-US", { minimumFractionDigits: 1, maximumFractionDigits: 2 })}%`;
  }

  private _formatRate(value: number): string {
    return `${(value * 100).toLocaleString("en-US", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}%`;
  }

  private _formatEntryMode(value: string | null | undefined): string {
    switch (value) {
      case "WaitForLimitPrice":
        return "Wait for limit price";
      case "InitialMarketThenGrid":
        return "Initial market buy, then grid";
      default:
        return "Auto from signal candle";
    }
  }

  private _formatLimitPrice(entryMode: string | null | undefined, value: number | null | undefined): string {
    return entryMode === "WaitForLimitPrice" && value !== null && value !== undefined
      ? this._formatCurrency(value)
      : "—";
  }

  private _formatDate(value: string): string {
    return new Date(value).toISOString().slice(0, 10);
  }

  private _formatDuration(totalMinutes: number): string {
    const roundedMinutes = Math.max(0, Math.round(totalMinutes));

    if (roundedMinutes < 60) {
      return `${roundedMinutes}m`;
    }

    const hours = Math.floor(roundedMinutes / 60);
    const minutes = roundedMinutes % 60;

    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
}