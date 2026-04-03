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
  HistogramData,
  HistogramSeries,
  IChartApi,
  IPriceLine,
  ISeriesApi,
  ISeriesMarkersPluginApi,
  LineData,
  LineSeries,
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

type IndicatorToggleKey = "emaFast" | "emaSlow" | "emaTrend" | "bollinger" | "rsi" | "macd";

interface IndicatorToggleState {
  emaFast: boolean;
  emaSlow: boolean;
  emaTrend: boolean;
  bollinger: boolean;
  rsi: boolean;
  macd: boolean;
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

  @ViewChild("rsiChartContainer", { static: true })
  private readonly _rsiChartContainer!: ElementRef<HTMLDivElement>;

  @ViewChild("macdChartContainer", { static: true })
  private readonly _macdChartContainer!: ElementRef<HTMLDivElement>;

  public indicatorToggles: IndicatorToggleState = {
    emaFast: true,
    emaSlow: true,
    emaTrend: false,
    bollinger: false,
    rsi: false,
    macd: false,
  };

  private _chart: IChartApi | null = null;
  private _candleSeries: ISeriesApi<"Candlestick"> | null = null;
  private _markersApi: ISeriesMarkersPluginApi<Time> | null = null;
  private _resizeObserver: ResizeObserver | null = null;
  private _priceLines: IPriceLine[] = [];
  private _emaFastSeries: ISeriesApi<"Line"> | null = null;
  private _emaSlowSeries: ISeriesApi<"Line"> | null = null;
  private _emaTrendSeries: ISeriesApi<"Line"> | null = null;
  private _bollingerUpperSeries: ISeriesApi<"Line"> | null = null;
  private _bollingerMiddleSeries: ISeriesApi<"Line"> | null = null;
  private _bollingerLowerSeries: ISeriesApi<"Line"> | null = null;
  private _rsiChart: IChartApi | null = null;
  private _rsiSeries: ISeriesApi<"Line"> | null = null;
  private _rsiPriceLines: IPriceLine[] = [];
  private _macdChart: IChartApi | null = null;
  private _macdLineSeries: ISeriesApi<"Line"> | null = null;
  private _macdSignalSeries: ISeriesApi<"Line"> | null = null;
  private _macdHistogramSeries: ISeriesApi<"Histogram"> | null = null;

  public get hasData(): boolean {
    return (this.debugData?.candleEvaluations.length ?? 0) > 0;
  }

  public ngAfterViewInit(): void {
    this._initChart();
    this._initRsiChart();
    this._initMacdChart();
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
    const rsiChart = this._rsiChart;
    const macdChart = this._macdChart;

    this._resizeObserver = null;
    this._markersApi = null;
    this._priceLines = [];
    this._emaFastSeries = null;
    this._emaSlowSeries = null;
    this._emaTrendSeries = null;
    this._bollingerUpperSeries = null;
    this._bollingerMiddleSeries = null;
    this._bollingerLowerSeries = null;
    this._candleSeries = null;
    this._chart = null;
    this._rsiPriceLines = [];
    this._rsiSeries = null;
    this._rsiChart = null;
    this._macdHistogramSeries = null;
    this._macdLineSeries = null;
    this._macdSignalSeries = null;
    this._macdChart = null;

    resizeObserver?.disconnect();
    chart?.remove();
    rsiChart?.remove();
    macdChart?.remove();
  }

  public onIndicatorToggleChanged(key: IndicatorToggleKey, checked: boolean): void {
    this.indicatorToggles = {
      ...this.indicatorToggles,
      [key]: checked,
    };

    this._refreshIndicatorSeries();
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
    this._emaFastSeries = this._chart.addSeries(LineSeries, {
      color: "#38bdf8",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._emaSlowSeries = this._chart.addSeries(LineSeries, {
      color: "#f97316",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._emaTrendSeries = this._chart.addSeries(LineSeries, {
      color: "#22c55e",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerUpperSeries = this._chart.addSeries(LineSeries, {
      color: "rgba(168, 85, 247, 0.85)",
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerMiddleSeries = this._chart.addSeries(LineSeries, {
      color: "rgba(244, 114, 182, 0.8)",
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerLowerSeries = this._chart.addSeries(LineSeries, {
      color: "rgba(168, 85, 247, 0.85)",
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });

    this._resizeObserver = new ResizeObserver((entries: ResizeObserverEntry[]) => {
      for (const entry of entries) {
        this._chart?.applyOptions({ width: Math.max(entry.contentRect.width, 320) });
        this._rsiChart?.applyOptions({ width: Math.max(entry.contentRect.width, 320) });
        this._macdChart?.applyOptions({ width: Math.max(entry.contentRect.width, 320) });
      }
    });

    this._resizeObserver.observe(container);
  }

  private _initRsiChart(): void {
    const container = this._rsiChartContainer.nativeElement;

    this._rsiChart = createChart(container, {
      width: Math.max(container.clientWidth, 320),
      height: 140,
      layout: {
        background: { color: "#101827" },
        textColor: "#94a3b8"
      },
      grid: {
        vertLines: { color: "#1f2937" },
        horzLines: { color: "#1f2937" }
      },
      timeScale: {
        visible: false,
        borderColor: "#1f2937"
      },
      rightPriceScale: {
        borderColor: "#1f2937"
      }
    });

    this._rsiSeries = this._rsiChart.addSeries(LineSeries, {
      color: "#facc15",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });

    this._rsiPriceLines = [
      this._rsiSeries.createPriceLine({ price: 70, color: "rgba(239, 68, 68, 0.7)", lineWidth: 1, lineStyle: 2, axisLabelVisible: true, title: "70" }),
      this._rsiSeries.createPriceLine({ price: 30, color: "rgba(34, 197, 94, 0.7)", lineWidth: 1, lineStyle: 2, axisLabelVisible: true, title: "30" })
    ];
  }

  private _initMacdChart(): void {
    const container = this._macdChartContainer.nativeElement;

    this._macdChart = createChart(container, {
      width: Math.max(container.clientWidth, 320),
      height: 160,
      layout: {
        background: { color: "#101827" },
        textColor: "#94a3b8"
      },
      grid: {
        vertLines: { color: "#1f2937" },
        horzLines: { color: "#1f2937" }
      },
      timeScale: {
        timeVisible: true,
        secondsVisible: false,
        borderColor: "#1f2937"
      },
      rightPriceScale: {
        borderColor: "#1f2937"
      }
    });

    this._macdHistogramSeries = this._macdChart.addSeries(HistogramSeries, {
      priceLineVisible: false,
      lastValueVisible: false,
    });
    this._macdLineSeries = this._macdChart.addSeries(LineSeries, {
      color: "#38bdf8",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._macdSignalSeries = this._macdChart.addSeries(LineSeries, {
      color: "#f97316",
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
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
    this._refreshIndicatorSeries();

    if (candles.length > 0) {
      this._chart.timeScale().fitContent();
      const range = this._chart.timeScale().getVisibleLogicalRange();
      if (range) {
        this._rsiChart?.timeScale().setVisibleLogicalRange(range);
        this._macdChart?.timeScale().setVisibleLogicalRange(range);
      }
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

    for (const priceLine of this._priceLines) {
      this._candleSeries.removePriceLine(priceLine);
    }

    this._priceLines = [];
  }

  private _addGridOverlay(summary: GridCycleSummary | null): void {
    if (!this._candleSeries || !summary) {
      return;
    }

    // Anchor price line
    this._priceLines.push(this._candleSeries.createPriceLine({
      price: summary.anchorPrice,
      color: "#f59e0b",
      lineWidth: 2,
      lineStyle: 0, // Solid
      axisLabelVisible: true,
      title: "Anchor"
    }));

    // Grid level lines
    for (let i = 0; i < summary.levelPrices.length; i++) {
      const isFilled = this._isFilledLevel(summary, i);
      this._priceLines.push(this._candleSeries.createPriceLine({
        price: summary.levelPrices[i],
        color: isFilled ? "#26a69a" : "rgba(96, 165, 250, 0.4)",
        lineWidth: 1,
        lineStyle: isFilled ? 0 : 2, // Solid if filled, dashed if not
        axisLabelVisible: false,
        title: isFilled ? `L${i + 1} ✓` : `L${i + 1}`
      }));
    }

    // Take profit line
    if (summary.takeProfitPrice > 0) {
      this._priceLines.push(this._candleSeries.createPriceLine({
        price: summary.takeProfitPrice,
        color: "#22c55e",
        lineWidth: 2,
        lineStyle: 2, // Dashed
        axisLabelVisible: true,
        title: "TP"
      }));
    }

    // Stop loss line
    if (summary.stopLossPrice && summary.stopLossPrice > 0) {
      this._priceLines.push(this._candleSeries.createPriceLine({
        price: summary.stopLossPrice,
        color: "#ef5350",
        lineWidth: 2,
        lineStyle: 2, // Dashed
        axisLabelVisible: true,
        title: "SL"
      }));
    }
  }

  private _isFilledLevel(summary: GridCycleSummary, levelIndex: number): boolean {
    const firstFilledIndex = Math.max(0, summary.levelPrices.length - summary.levelsFilled);
    return levelIndex >= firstFilledIndex;
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

  private _refreshIndicatorSeries(): void {
    const evaluations = this.debugData?.candleEvaluations ?? [];
    this._setLineSeriesData(this._emaFastSeries, this.indicatorToggles.emaFast ? this._mapIndicatorLineData(evaluations, candle => candle.emaFast) : []);
    this._setLineSeriesData(this._emaSlowSeries, this.indicatorToggles.emaSlow ? this._mapIndicatorLineData(evaluations, candle => candle.emaSlow) : []);
    this._setLineSeriesData(this._emaTrendSeries, this.indicatorToggles.emaTrend ? this._mapIndicatorLineData(evaluations, candle => candle.emaTrend) : []);

    const showBollinger = this.indicatorToggles.bollinger;
    this._setLineSeriesData(this._bollingerUpperSeries, showBollinger ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.bollingerUpper) : []);
    this._setLineSeriesData(this._bollingerMiddleSeries, showBollinger ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.bollingerMiddle) : []);
    this._setLineSeriesData(this._bollingerLowerSeries, showBollinger ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.bollingerLower) : []);

    this._setLineSeriesData(this._rsiSeries, this.indicatorToggles.rsi ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.rsi ?? candle.rsi) : []);
    this._setLineSeriesData(this._macdLineSeries, this.indicatorToggles.macd ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.macdLine) : []);
    this._setLineSeriesData(this._macdSignalSeries, this.indicatorToggles.macd ? this._mapIndicatorLineData(evaluations, candle => candle.indicators?.macdSignal) : []);
    this._setHistogramSeriesData(this._macdHistogramSeries, this.indicatorToggles.macd ? this._mapMacdHistogramData(evaluations) : []);
  }

  private _mapIndicatorLineData(
    evaluations: CandleEvaluation[],
    selector: (candle: CandleEvaluation) => number | null | undefined): LineData<UTCTimestamp>[] {
    return evaluations
      .filter((candle: CandleEvaluation) => !candle.isWarmup && selector(candle) != null)
      .map((candle: CandleEvaluation) => ({
        time: Math.floor(candle.timestampUtc / 1000) as UTCTimestamp,
        value: selector(candle) as number,
      }));
  }

  private _mapMacdHistogramData(evaluations: CandleEvaluation[]): HistogramData<UTCTimestamp>[] {
    return evaluations
      .filter((candle: CandleEvaluation) => !candle.isWarmup && candle.indicators?.macdHistogram != null)
      .map((candle: CandleEvaluation) => {
        const value = candle.indicators?.macdHistogram as number;
        return {
          time: Math.floor(candle.timestampUtc / 1000) as UTCTimestamp,
          value,
          color: value >= 0 ? "rgba(34, 197, 94, 0.8)" : "rgba(239, 68, 68, 0.8)",
        };
      });
  }

  private _setLineSeriesData(series: ISeriesApi<"Line"> | null, data: LineData<UTCTimestamp>[]): void {
    series?.setData(data);
  }

  private _setHistogramSeriesData(series: ISeriesApi<"Histogram"> | null, data: HistogramData<UTCTimestamp>[]): void {
    series?.setData(data);
  }
}
