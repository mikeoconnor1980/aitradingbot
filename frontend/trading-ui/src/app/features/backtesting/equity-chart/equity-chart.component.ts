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

interface EquityDataPoint {
  time: UTCTimestamp;
  value: number;
}

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

    if (changes["equityData"] || changes["trades"] || changes["comparisonData"]) {
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
        background: { color: "#1a1a2e" },
        textColor: "#a0a0b0"
      },
      grid: {
        vertLines: { color: "#2a2a3e" },
        horzLines: { color: "#2a2a3e" }
      },
      crosshair: {
        mode: CrosshairMode.Normal
      },
      timeScale: {
        borderColor: "#2a2a3e",
        timeVisible: true,
        secondsVisible: false
      },
      rightPriceScale: {
        borderColor: "#2a2a3e"
      }
    });

    this._primarySeries = this._chart.addSeries(AreaSeries, {
      lineColor: "#26a69a",
      topColor: "rgba(38, 166, 154, 0.35)",
      bottomColor: "rgba(38, 166, 154, 0.02)",
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
          lineColor: "#60a5fa",
          topColor: "rgba(96, 165, 250, 0.18)",
          bottomColor: "rgba(96, 165, 250, 0.02)",
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
    return data.map((snapshot: EquitySnapshot) => ({
      time: Math.floor(snapshot.timestampUtc / 1000) as UTCTimestamp,
      value: snapshot.equity
    }));
  }

  private _buildMarkers(): SeriesMarker<Time>[] {
    return this.trades
      .flatMap((trade: BacktestTrade) => {
        const markers: SeriesMarker<Time>[] = [
          {
            time: this._toUtcTimestamp(trade.entryTime),
            position: "belowBar",
            color: trade.side === "Long" ? "#26a69a" : "#60a5fa",
            shape: trade.side === "Long" ? "arrowUp" : "arrowDown",
            text: `${trade.side} entry`
          }
        ];

        if (trade.exitTime) {
          markers.push({
            time: this._toUtcTimestamp(trade.exitTime),
            position: "aboveBar",
            color: (trade.pnl ?? 0) >= 0 ? "#26a69a" : "#ef5350",
            shape: "arrowDown",
            text: trade.pnl != null
              ? `Exit ${trade.pnl >= 0 ? "+" : ""}${trade.pnl.toFixed(2)}`
              : "Exit"
          });
        }

        return markers;
      })
      .sort((left: SeriesMarker<Time>, right: SeriesMarker<Time>) => {
        return (left.time as number) - (right.time as number);
      });
  }

  private _toUtcTimestamp(value: string): UTCTimestamp {
    return Math.floor(new Date(value).getTime() / 1000) as UTCTimestamp;
  }
}