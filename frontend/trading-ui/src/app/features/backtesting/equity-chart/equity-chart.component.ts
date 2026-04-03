import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild
} from "@angular/core";
import {
  AreaSeries,
  createChart,
  createSeriesMarkers,
  CrosshairMode,
  IChartApi,
  ISeriesApi,
  ISeriesMarkersPluginApi,
  SeriesMarker,
  Time,
  UTCTimestamp
} from "lightweight-charts";
import { BacktestTrade, EquitySnapshot } from "../../../core/models/backtest.model";
import { GridCycleSummary } from "../../../core/models/backtest-debug.model";

interface EquityDataPoint {
  time: UTCTimestamp;
  value: number;
}

const EQUITY_CHART_THEME = {
  background: "#091315",
  grid: "#162629",
  text: "#7f9d99",
  profit: "#3bc9a8",
  loss: "#e07a8f",
  warningStrong: "#b9873f",
  comparison: "#8fc7d8",
  profitFillTop: "rgba(59, 201, 168, 0.24)",
  profitFillBottom: "rgba(59, 201, 168, 0.02)",
  comparisonFillTop: "rgba(143, 199, 216, 0.12)",
  comparisonFillBottom: "rgba(143, 199, 216, 0.02)"
} as const;

@Component({
  selector: "app-equity-chart",
  standalone: true,
  imports: [],
  templateUrl: "./equity-chart.component.html",
  styleUrl: "./equity-chart.component.scss"
})
export class EquityChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input()
  public equityData: EquitySnapshot[] = [];

  @Input()
  public trades: BacktestTrade[] = [];

  @Input()
  public cycleSummaries: GridCycleSummary[] = [];

  @Input()
  public comparisonData: EquitySnapshot[] | null = null;

  @Input()
  public primaryLabel = "Equity";

  @Input()
  public comparisonLabel = "Comparison";

  public get hasEquityData(): boolean {
    return this.equityData.length > 0;
  }

  @ViewChild("chartContainer", { static: true })
  private readonly _chartContainer!: ElementRef<HTMLDivElement>;

  private _chart: IChartApi | null = null;
  private _primarySeries: ISeriesApi<"Area"> | null = null;
  private _comparisonSeries: ISeriesApi<"Area"> | null = null;
  private _markersApi: ISeriesMarkersPluginApi<Time> | null = null;
  private _resizeObserver: ResizeObserver | null = null;

  public ngAfterViewInit(): void {
    this._initChart();
    this._updateData();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (!this._chart) {
      return;
    }

    if (changes["primaryLabel"] && this._primarySeries) {
      this._primarySeries.applyOptions({ title: this.primaryLabel });
    }

    if (changes["comparisonLabel"] && this._comparisonSeries) {
      this._comparisonSeries.applyOptions({ title: this.comparisonLabel });
    }

    if (changes["equityData"] || changes["trades"] || changes["comparisonData"] || changes["cycleSummaries"]) {
      this._updateData();
    }
  }

  public ngOnDestroy(): void {
    const resizeObserver = this._resizeObserver;
    const chart = this._chart;

    this._resizeObserver = null;
    this._markersApi = null;
    this._comparisonSeries = null;
    this._primarySeries = null;
    this._chart = null;

    resizeObserver?.disconnect();
    chart?.remove();
  }

  private _initChart(): void {
    const container = this._chartContainer.nativeElement;

    this._chart = createChart(container, {
      width: Math.max(container.clientWidth, 320),
      height: 400,
      layout: {
        background: { color: EQUITY_CHART_THEME.background },
        textColor: EQUITY_CHART_THEME.text
      },
      grid: {
        vertLines: { color: EQUITY_CHART_THEME.grid },
        horzLines: { color: EQUITY_CHART_THEME.grid }
      },
      crosshair: {
        mode: CrosshairMode.Normal
      },
      timeScale: {
        borderColor: EQUITY_CHART_THEME.grid,
        timeVisible: true,
        secondsVisible: false
      },
      rightPriceScale: {
        borderColor: EQUITY_CHART_THEME.grid
      }
    });

    this._primarySeries = this._chart.addSeries(AreaSeries, {
      lineColor: EQUITY_CHART_THEME.profit,
      topColor: EQUITY_CHART_THEME.profitFillTop,
      bottomColor: EQUITY_CHART_THEME.profitFillBottom,
      lineWidth: 2,
      priceLineVisible: false,
      title: this.primaryLabel
    });

    this._markersApi = createSeriesMarkers(this._primarySeries, []);

    this._resizeObserver = new ResizeObserver((entries: ResizeObserverEntry[]) => {
      for (const entry of entries) {
        this._chart?.applyOptions({ width: Math.max(entry.contentRect.width, 320) });
      }
    });

    this._resizeObserver.observe(container);
  }

  private _updateData(): void {
    if (!this._chart || !this._primarySeries) {
      return;
    }

    const primaryData = this._mapEquityData(this.equityData);
    this._primarySeries.setData(primaryData);
    this._markersApi?.setMarkers(this._buildMarkers());

    if (this.comparisonData && this.comparisonData.length > 0) {
      if (!this._comparisonSeries) {
        this._comparisonSeries = this._chart.addSeries(AreaSeries, {
          lineColor: EQUITY_CHART_THEME.comparison,
          topColor: EQUITY_CHART_THEME.comparisonFillTop,
          bottomColor: EQUITY_CHART_THEME.comparisonFillBottom,
          lineWidth: 2,
          priceLineVisible: false,
          title: this.comparisonLabel
        });
      }

      this._comparisonSeries.setData(this._mapEquityData(this.comparisonData));
    } else if (this._comparisonSeries) {
      this._chart.removeSeries(this._comparisonSeries);
      this._comparisonSeries = null;
    }

    if (primaryData.length > 0) {
      this._chart.timeScale().fitContent();
    }
  }

  private _mapEquityData(data: EquitySnapshot[]): EquityDataPoint[] {
    const deduped = new Map<number, number>();

    for (const snapshot of data) {
      const time = Math.floor(snapshot.timestampUtc / 1000);
      deduped.set(time, snapshot.equity);
    }

    return Array.from(deduped, ([time, value]) => ({
      time: time as UTCTimestamp,
      value
    }));
  }

  private _buildMarkers(): SeriesMarker<Time>[] {
    const markers: SeriesMarker<Time>[] = [];

    // Add lifecycle annotations from cycle summaries
    for (const summary of this.cycleSummaries) {
      // Grid deployed marker
      markers.push({
        time: (Math.floor(summary.deployTimestampUtc / 1000)) as UTCTimestamp,
        position: "belowBar",
        color: EQUITY_CHART_THEME.warningStrong,
        shape: "circle",
        text: `Grid deployed`
      });

      // Grid exit marker
      if (summary.closeTimestampUtc > 0) {
        const exitLabel = this._getExitLabel(summary);
        const exitColor = summary.cyclePnl >= 0 ? EQUITY_CHART_THEME.profit : EQUITY_CHART_THEME.loss;
        markers.push({
          time: (Math.floor(summary.closeTimestampUtc / 1000)) as UTCTimestamp,
          position: "aboveBar",
          color: exitColor,
          shape: "circle",
          text: exitLabel
        });
      }
    }

    // Add trade entry/exit markers
    for (const trade of this.trades) {
      markers.push({
        time: this._toUtcTimestamp(trade.entryTime),
        position: "belowBar",
        color: trade.side === "Long" ? EQUITY_CHART_THEME.profit : EQUITY_CHART_THEME.comparison,
        shape: trade.side === "Long" ? "arrowUp" : "arrowDown",
        text: `${trade.side} entry`
      });

      if (trade.exitTime) {
        markers.push({
          time: this._toUtcTimestamp(trade.exitTime),
          position: "aboveBar",
          color: (trade.pnl ?? 0) >= 0 ? EQUITY_CHART_THEME.profit : EQUITY_CHART_THEME.loss,
          shape: "arrowDown",
          text: trade.pnl != null
            ? `Exit ${trade.pnl >= 0 ? "+" : ""}${trade.pnl.toFixed(2)}`
            : "Exit"
        });
      }
    }

    // Deduplicate markers at the same timestamp by keeping the most informative one
    const byTime = new Map<number, SeriesMarker<Time>[]>();
    for (const marker of markers) {
      const key = marker.time as number;
      const existing = byTime.get(key) ?? [];
      existing.push(marker);
      byTime.set(key, existing);
    }

    const deduped: SeriesMarker<Time>[] = [];
    for (const group of byTime.values()) {
      // Prefer circle (lifecycle) markers, then keep first of each position
      const aboveBar = group.filter((m) => m.position === "aboveBar");
      const belowBar = group.filter((m) => m.position === "belowBar");

      if (belowBar.length > 0) {
        deduped.push(belowBar.find((m) => m.shape === "circle") ?? belowBar[0]);
      }
      if (aboveBar.length > 0) {
        deduped.push(aboveBar.find((m) => m.shape === "circle") ?? aboveBar[0]);
      }
    }

    return deduped.sort(
      (left: SeriesMarker<Time>, right: SeriesMarker<Time>) => (left.time as number) - (right.time as number)
    );
  }

  private _getExitLabel(summary: GridCycleSummary): string {
    const pnl = summary.cyclePnl;
    const pnlStr = pnl >= 0 ? `+$${pnl.toFixed(2)}` : `-$${Math.abs(pnl).toFixed(2)}`;

    switch (summary.exitReason) {
      case "TakeProfit":
        return `TP hit ${pnlStr}`;
      case "StopLoss":
        return `SL hit ${pnlStr}`;
      case "Breakdown":
        return `Breakdown ${pnlStr}`;
      default:
        return `Exit ${pnlStr}`;
    }
  }

  private _toUtcTimestamp(value: string): UTCTimestamp {
    return Math.floor(new Date(value).getTime() / 1000) as UTCTimestamp;
  }
}