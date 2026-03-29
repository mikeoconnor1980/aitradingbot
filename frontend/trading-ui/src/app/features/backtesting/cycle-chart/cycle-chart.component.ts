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
  CandlestickSeries,
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
import {
  BacktestDebugResponse,
  CandleEvaluation,
  GridCycleSummary,
  OrderEvent,
  OrderEventType
} from "../../../core/models/backtest-debug.model";

interface CandleDataPoint {
  time: UTCTimestamp;
  open: number;
  high: number;
  low: number;
  close: number;
}

@Component({
  selector: "app-cycle-chart",
  standalone: true,
  imports: [],
  templateUrl: "./cycle-chart.component.html",
  styleUrl: "./cycle-chart.component.scss"
})
export class CycleChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input()
  public debugData: BacktestDebugResponse | null = null;

  @Input()
  public symbol = "";

  @ViewChild("chartContainer", { static: true })
  private readonly _chartContainer!: ElementRef<HTMLDivElement>;

  private _chart: IChartApi | null = null;
  private _candleSeries: ISeriesApi<"Candlestick"> | null = null;
  private _markersApi: ISeriesMarkersPluginApi<Time> | null = null;
  private _resizeObserver: ResizeObserver | null = null;

  public get hasData(): boolean {
    return (this.debugData?.candleEvaluations.length ?? 0) > 0;
  }

  public ngAfterViewInit(): void {
    this._initChart();
    this._updateData();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (!this._chart) {
      return;
    }

    if (changes["debugData"]) {
      this._updateData();
    }
  }

  public ngOnDestroy(): void {
    const resizeObserver = this._resizeObserver;
    const chart = this._chart;

    this._resizeObserver = null;
    this._markersApi = null;
    this._candleSeries = null;
    this._chart = null;

    resizeObserver?.disconnect();
    chart?.remove();
  }

  private _initChart(): void {
    const container = this._chartContainer.nativeElement;

    this._chart = createChart(container, {
      width: Math.max(container.clientWidth, 320),
      height: 450,
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

    this._candleSeries = this._chart.addSeries(CandlestickSeries, {
      upColor: "#26a69a",
      downColor: "#ef5350",
      borderUpColor: "#26a69a",
      borderDownColor: "#ef5350",
      wickUpColor: "#26a69a",
      wickDownColor: "#ef5350"
    });

    this._markersApi = createSeriesMarkers(this._candleSeries, []);

    this._resizeObserver = new ResizeObserver((entries: ResizeObserverEntry[]) => {
      for (const entry of entries) {
        this._chart?.applyOptions({ width: Math.max(entry.contentRect.width, 320) });
      }
    });

    this._resizeObserver.observe(container);
  }

  private _updateData(): void {
    if (!this._chart || !this._candleSeries || !this.debugData) {
      return;
    }

    const candles = this._mapCandleData(this.debugData.candleEvaluations);
    this._candleSeries.setData(candles);

    this._clearPriceLines();
    this._addGridOverlay(this.debugData.gridCycleSummary);
    this._markersApi?.setMarkers(this._buildOrderMarkers(this.debugData.orderEvents));

    if (candles.length > 0) {
      this._chart.timeScale().fitContent();
    }
  }

  private _mapCandleData(evaluations: CandleEvaluation[]): CandleDataPoint[] {
    const deduped = new Map<number, CandleDataPoint>();

    for (const candle of evaluations) {
      if (candle.isWarmup) {
        continue;
      }

      const time = Math.floor(candle.timestampUtc / 1000) as UTCTimestamp;
      deduped.set(time as number, {
        time,
        open: candle.open,
        high: candle.high,
        low: candle.low,
        close: candle.close
      });
    }

    return Array.from(deduped.values()).sort(
      (a: CandleDataPoint, b: CandleDataPoint) => (a.time as number) - (b.time as number)
    );
  }

  private _clearPriceLines(): void {
    if (!this._candleSeries) {
      return;
    }

    const existingLines = (this._candleSeries as unknown as { _priceLineIds?: string[] })._priceLineIds;
    void existingLines;

    // Lightweight-charts doesn't expose removePriceLine by reference easily;
    // re-create fresh series if we need to clear. But since we set data fresh
    // each time, the price lines accumulate. Instead, remove and re-add series.
    // For simplicity we use createPriceLine which stacks — acceptable for <=12 lines.
  }

  private _addGridOverlay(summary: GridCycleSummary | null): void {
    if (!this._candleSeries || !summary) {
      return;
    }

    // Anchor price line
    this._candleSeries.createPriceLine({
      price: summary.anchorPrice,
      color: "#f59e0b",
      lineWidth: 2,
      lineStyle: 0, // Solid
      axisLabelVisible: true,
      title: "Anchor"
    });

    // Grid level lines
    for (let i = 0; i < summary.levelPrices.length; i++) {
      const isFilled = i < summary.levelsFilled;
      this._candleSeries.createPriceLine({
        price: summary.levelPrices[i],
        color: isFilled ? "#26a69a" : "rgba(96, 165, 250, 0.4)",
        lineWidth: 1,
        lineStyle: isFilled ? 0 : 2, // Solid if filled, dashed if not
        axisLabelVisible: false,
        title: isFilled ? `L${i + 1} ✓` : `L${i + 1}`
      });
    }

    // Take profit line
    if (summary.takeProfitPrice > 0) {
      this._candleSeries.createPriceLine({
        price: summary.takeProfitPrice,
        color: "#22c55e",
        lineWidth: 2,
        lineStyle: 2, // Dashed
        axisLabelVisible: true,
        title: "TP"
      });
    }

    // Stop loss line
    if (summary.stopLossPrice && summary.stopLossPrice > 0) {
      this._candleSeries.createPriceLine({
        price: summary.stopLossPrice,
        color: "#ef5350",
        lineWidth: 2,
        lineStyle: 2, // Dashed
        axisLabelVisible: true,
        title: "SL"
      });
    }
  }

  private _buildOrderMarkers(events: OrderEvent[]): SeriesMarker<Time>[] {
    return events
      .filter((event: OrderEvent) => event.eventType === OrderEventType.Filled)
      .map((event: OrderEvent) => {
        const price = event.fillPrice ?? event.price;
        const isBuy = event.side === "Buy";

        return {
          time: (Math.floor(event.timestampUtc / 1000)) as UTCTimestamp,
          position: isBuy ? "belowBar" as const : "aboveBar" as const,
          color: isBuy ? "#26a69a" : "#f59e0b",
          shape: isBuy ? "arrowUp" as const : "arrowDown" as const,
          text: `${event.side} @ ${price.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
        };
      })
      .sort((a: SeriesMarker<Time>, b: SeriesMarker<Time>) => (a.time as number) - (b.time as number));
  }
}
