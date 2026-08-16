import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  inject
} from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  CandlestickData,
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
  LogicalRange,
  MouseEventParams,
  SeriesMarker,
  Time,
  UTCTimestamp
} from "lightweight-charts";
import { AnalystChartContext, ChartIndicatorId } from "../../../core/models/analyst.model";
import { Candle } from "../../../core/models/candle.model";
import { ChartIndicatorValues } from "../../../core/models/chart-indicator.model";
import { FillEvent } from "../../../core/models/fill-event.model";
import { PriceUpdate } from "../../../core/models/price-update.model";
import { SignalRService } from "../../../core/services/signalr.service";

interface ConsolidatedFillGroup {
  time: number;
  side: string;
  fills: FillEvent[];
  totalSize: number;
  totalFee: number;
  totalClosedPnl: number;
  averagePrice: number;
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

const PRICE_CHART_THEME = {
  background: "#091315",
  panelBackground: "#071012",
  grid: "#162629",
  text: "#7f9d99",
  accent: "#79cfc3",
  info: "#8fc7d8",
  profit: "#3bc9a8",
  loss: "#e07a8f",
  warning: "#caa86a",
  warningStrong: "#b9873f",
  band: "rgba(143, 199, 216, 0.42)",
  bandMid: "rgba(224, 122, 143, 0.4)",
  rsiOverbought: "rgba(224, 122, 143, 0.55)",
  rsiOversold: "rgba(59, 201, 168, 0.55)"
} as const;

@Component({
  selector: "app-price-chart",
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  templateUrl: "./price-chart.component.html",
  styleUrl: "./price-chart.component.scss"
})
export class PriceChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  private static readonly TIMEFRAME_SECONDS: Record<string, number> = {
    "1m": 60, "3m": 180, "5m": 300, "15m": 900, "30m": 1800,
    "1h": 3600, "4h": 14400, "1d": 86400
  };

  @Input() public seedCandles: Candle[] = [];
  @Input() public selectedAsset = "BTC-PERP";
  @Input() public selectedTimeframe = "15m";
  @Input() public fills: FillEvent[] = [];
  @Input() public showTradeMarkers = true;

  @Output() public loadMoreCandles = new EventEmitter<number>();

  public tooltipGroups: ConsolidatedFillGroup[] = [];
  public tooltipVisible = false;
  public tooltipLeft = 0;
  public tooltipTop = 0;
  public selectedCandleOpenTimeUtc: string | null = null;

  public get timeWindowLabel(): string {
    if (!this._candles.length) return "";
    const first = this._candles[0].time as number;
    const last = this._candles[this._candles.length - 1].time as number;
    const diffHours = Math.round((last - first) / 3600);
    if (diffHours < 48) return `${diffHours}H`;
    return `${Math.round(diffHours / 24)}D`;
  }

  private get _candleSeconds(): number {
    return PriceChartComponent.TIMEFRAME_SECONDS[this.selectedTimeframe] ?? 900;
  }

  @ViewChild("chartContainer", { static: true })
  private readonly _chartContainer!: ElementRef<HTMLDivElement>;

  @ViewChild("rsiChartContainer", { static: true })
  private readonly _rsiChartContainer!: ElementRef<HTMLDivElement>;

  @ViewChild("macdChartContainer", { static: true })
  private readonly _macdChartContainer!: ElementRef<HTMLDivElement>;

  private readonly _signalRService = inject(SignalRService);
  private readonly _destroyRef = inject(DestroyRef);

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
  private _candles: CandlestickData<UTCTimestamp>[] = [];
  private _indicatorCandles: Candle[] = [];
  private _currentFills: FillEvent[] = [];
  private _fillGroupsByTime = new Map<number, ConsolidatedFillGroup[]>();
  private _liveCandle: CandlestickData<UTCTimestamp> | null = null;
  private _isLoadingHistory = false;
  private _oldestTimestamp: number | null = null;
  private _crosshairHandler: ((param: MouseEventParams<Time>) => void) | null = null;
  private _clickHandler: ((param: MouseEventParams<Time>) => void) | null = null;
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

  public ngAfterViewInit(): void {
    this._initChart();
    this._initRsiChart();
    this._initMacdChart();
    this._applySeedCandles();
    this._subscribeToUpdates();
    this._subscribeToVisibleRangeChange();
    this._subscribeCrosshairMove();
    this._subscribeClick();
    this._refreshMarkers();
    this._refreshIndicatorSeries();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["fills"]) {
      this._currentFills = [...this.fills];
      this._refreshMarkers();
    }

    if (changes["showTradeMarkers"]) {
      if (!this.showTradeMarkers) {
        this._hideFillTooltip();
      }

      this._refreshMarkers();
    }

    if (changes["seedCandles"] || changes["selectedTimeframe"] || changes["selectedAsset"]) {
      if (changes["selectedTimeframe"] || changes["selectedAsset"]) {
        this.clearSelectedCandle();
      }
      this._liveCandle = null;
      this._isLoadingHistory = false;
      this._oldestTimestamp = null;
      this._hideFillTooltip();
      this._applySeedCandles();
    }
  }

  public ngOnDestroy(): void {
    const resizeObserver = this._resizeObserver;
    const chart = this._chart;
    const rsiChart = this._rsiChart;
    const macdChart = this._macdChart;

    if (this._crosshairHandler) {
      chart?.unsubscribeCrosshairMove(this._crosshairHandler);
    }

    if (this._clickHandler) {
      chart?.unsubscribeClick(this._clickHandler);
    }

    this._crosshairHandler = null;
    this._clickHandler = null;
    this._resizeObserver = null;
    this._markersApi = null;
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

  public captureAnalystContext(): AnalystChartContext | null {
    const range = this._chart?.timeScale().getVisibleLogicalRange();
    if (!range || this._candles.length === 0) {
      return null;
    }

    const fromIndex = Math.max(0, Math.min(this._candles.length - 1, Math.floor(range.from)));
    const toIndex = Math.max(0, Math.min(this._candles.length - 1, Math.ceil(range.to)));
    if (fromIndex > toIndex) {
      return null;
    }

    const visibleFromOpenTimeUtc = this._toUtcIso(this._candles[fromIndex].time as number);
    const visibleToOpenTimeUtc = this._toUtcIso(this._candles[toIndex].time as number);
    const selectedCandleOpenTimeUtc = this.selectedCandleOpenTimeUtc &&
      this.selectedCandleOpenTimeUtc >= visibleFromOpenTimeUtc && this.selectedCandleOpenTimeUtc <= visibleToOpenTimeUtc
      ? this.selectedCandleOpenTimeUtc
      : undefined;
    return {
      symbol: this.selectedAsset,
      timeframe: this.selectedTimeframe,
      visibleFromOpenTimeUtc,
      visibleToOpenTimeUtc,
      selectedCandleOpenTimeUtc,
      activeIndicators: this._activeIndicators(),
      visibleOverlays: this.showTradeMarkers ? ["TRADE_MARKERS"] : [],
      capturedAtUtc: new Date().toISOString()
    };
  }

  public clearSelectedCandle(): void {
    this.selectedCandleOpenTimeUtc = null;
  }

  public prependCandles(candles: Candle[]): void {
    if (!candles.length) {
      this._isLoadingHistory = false;
      return;
    }

    const newData = candles
      .map((c) => ({
        time: Math.floor(c.timestamp / 1000) as UTCTimestamp,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
      }))
      .sort((a, b) => (a.time as number) - (b.time as number));

    // Deduplicate: only keep candles older than our current oldest
    const currentOldest = this._candles.length ? (this._candles[0].time as number) : Infinity;
    const unique = newData.filter(c => (c.time as number) < currentOldest);

    if (unique.length) {
      this._indicatorCandles = [...candles, ...this._indicatorCandles]
        .sort((left: Candle, right: Candle) => left.timestamp - right.timestamp)
        .filter((candle: Candle, index: number, all: Candle[]) => index === 0 || candle.timestamp !== all[index - 1].timestamp);
      this._candles = [...unique, ...this._candles];
      this._oldestTimestamp = this._candles[0].time as number;
      this._candleSeries?.setData(this._candles);
      this._refreshMarkers();
      this._refreshIndicatorSeries();
    }

    this._isLoadingHistory = false;
  }

  public addFill(fill: FillEvent): void {
    this._currentFills = [...this._currentFills, fill];
    this._refreshMarkers();
  }

  private _initChart(): void {
    const container = this._chartContainer.nativeElement;

    this._chart = createChart(container, {
      width: container.clientWidth,
      height: 300,
      layout: {
        background: { color: PRICE_CHART_THEME.background },
        textColor: PRICE_CHART_THEME.text
      },
      grid: {
        vertLines: { color: PRICE_CHART_THEME.grid },
        horzLines: { color: PRICE_CHART_THEME.grid }
      },
      timeScale: {
        timeVisible: true,
        secondsVisible: false,
        borderColor: PRICE_CHART_THEME.grid
      },
      rightPriceScale: {
        borderColor: PRICE_CHART_THEME.grid
      },
      crosshair: {
        mode: CrosshairMode.Normal
      }
    });

    this._candleSeries = this._chart.addSeries(CandlestickSeries, {
      upColor: PRICE_CHART_THEME.profit,
      downColor: PRICE_CHART_THEME.loss,
      borderVisible: false,
      wickUpColor: PRICE_CHART_THEME.profit,
      wickDownColor: PRICE_CHART_THEME.loss,
      priceFormat: {
        type: "price",
        precision: 2,
        minMove: 0.01
      }
    });

    this._markersApi = createSeriesMarkers(this._candleSeries, []);
    this._emaFastSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.accent,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._emaSlowSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.warningStrong,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._emaTrendSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.profit,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerUpperSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.band,
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerMiddleSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.bandMid,
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._bollingerLowerSeries = this._chart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.band,
      lineWidth: 1,
      lastValueVisible: false,
      priceLineVisible: false,
    });

    this._resizeObserver = new ResizeObserver((entries: ResizeObserverEntry[]) => {
      for (const entry of entries) {
        const { width } = entry.contentRect;
        this._chart?.applyOptions({ width });
        this._rsiChart?.applyOptions({ width });
        this._macdChart?.applyOptions({ width });
      }
    });

    this._resizeObserver.observe(container);
  }

  private _initRsiChart(): void {
    const container = this._rsiChartContainer.nativeElement;

    this._rsiChart = createChart(container, {
      width: container.clientWidth,
      height: 140,
      layout: {
        background: { color: PRICE_CHART_THEME.panelBackground },
        textColor: PRICE_CHART_THEME.text
      },
      grid: {
        vertLines: { color: PRICE_CHART_THEME.grid },
        horzLines: { color: PRICE_CHART_THEME.grid }
      },
      timeScale: {
        visible: false,
        borderColor: PRICE_CHART_THEME.grid
      },
      rightPriceScale: {
        borderColor: PRICE_CHART_THEME.grid
      }
    });

    this._rsiSeries = this._rsiChart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.warning,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });

    this._rsiPriceLines = [
      this._rsiSeries.createPriceLine({ price: 70, color: PRICE_CHART_THEME.rsiOverbought, lineWidth: 1, lineStyle: 2, axisLabelVisible: true, title: "70" }),
      this._rsiSeries.createPriceLine({ price: 30, color: PRICE_CHART_THEME.rsiOversold, lineWidth: 1, lineStyle: 2, axisLabelVisible: true, title: "30" })
    ];
  }

  private _initMacdChart(): void {
    const container = this._macdChartContainer.nativeElement;

    this._macdChart = createChart(container, {
      width: container.clientWidth,
      height: 160,
      layout: {
        background: { color: PRICE_CHART_THEME.panelBackground },
        textColor: PRICE_CHART_THEME.text
      },
      grid: {
        vertLines: { color: PRICE_CHART_THEME.grid },
        horzLines: { color: PRICE_CHART_THEME.grid }
      },
      timeScale: {
        timeVisible: true,
        secondsVisible: false,
        borderColor: PRICE_CHART_THEME.grid
      },
      rightPriceScale: {
        borderColor: PRICE_CHART_THEME.grid
      }
    });

    this._macdHistogramSeries = this._macdChart.addSeries(HistogramSeries, {
      priceLineVisible: false,
      lastValueVisible: false,
    });
    this._macdLineSeries = this._macdChart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.accent,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    this._macdSignalSeries = this._macdChart.addSeries(LineSeries, {
      color: PRICE_CHART_THEME.warningStrong,
      lineWidth: 2,
      lastValueVisible: false,
      priceLineVisible: false,
    });
  }

  private _applySeedCandles(): void {
    if (!this.seedCandles.length) {
      this._candles = [];
      this._oldestTimestamp = null;
      this._candleSeries?.setData([]);
      this._refreshMarkers();
      return;
    }

    const sorted = [...this.seedCandles].sort((a, b) => a.timestamp - b.timestamp);
    this._indicatorCandles = sorted;
    this._candles = sorted.map((c) => ({
      time: Math.floor(c.timestamp / 1000) as UTCTimestamp,
      open: c.open,
      high: c.high,
      low: c.low,
      close: c.close,
    }));
    this._oldestTimestamp = this._candles.length ? (this._candles[0].time as number) : null;
    this._candleSeries?.setData(this._candles);
    this._refreshMarkers();
    this._refreshIndicatorSeries();
    this._chart?.timeScale().fitContent();

    const range = this._chart?.timeScale().getVisibleLogicalRange();
    if (range) {
      this._rsiChart?.timeScale().setVisibleLogicalRange(range);
      this._macdChart?.timeScale().setVisibleLogicalRange(range);
    }
  }

  private _subscribeToUpdates(): void {
    this._signalRService.priceUpdate$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((update: PriceUpdate) => {
        this._updateLiveCandle(update);
      });
  }

  private _subscribeToVisibleRangeChange(): void {
    if (!this._chart) return;

    this._chart.timeScale().subscribeVisibleLogicalRangeChange((range: LogicalRange | null) => {
      if (!range || this._isLoadingHistory || !this._candles.length) return;

       this._rsiChart?.timeScale().setVisibleLogicalRange(range);
       this._macdChart?.timeScale().setVisibleLogicalRange(range);

      // When user scrolls to the left edge (first ~10 candles visible), load more
      if (range.from < 10) {
        this._isLoadingHistory = true;
        const oldestMs = (this._candles[0].time as number) * 1000;
        this.loadMoreCandles.emit(oldestMs);
      }
    });
  }

  private _updateLiveCandle(update: PriceUpdate): void {
    if (update.asset !== this.selectedAsset) return;
    const timeSeconds = Math.floor(update.timestamp / 1000);
    const candleTime = (Math.floor(timeSeconds / this._candleSeconds) * this._candleSeconds) as UTCTimestamp;
    const price = update.lastPrice;

    if (this._liveCandle && this._liveCandle.time === candleTime) {
      this._liveCandle.high = Math.max(this._liveCandle.high, price);
      this._liveCandle.low = Math.min(this._liveCandle.low, price);
      this._liveCandle.close = price;
    } else {
      if (this._liveCandle) {
        this._candles.push({ ...this._liveCandle });
        this._candleSeries?.setData(this._candles);
        this._refreshMarkers();
        this._refreshIndicatorSeries();
      }
      this._liveCandle = { time: candleTime, open: price, high: price, low: price, close: price };
      this._chart?.timeScale().scrollToRealTime();
    }

    if (this._liveCandle) {
      this._candleSeries?.update(this._liveCandle);
    }
  }

  private _refreshMarkers(): void {
    if (!this._markersApi) {
      return;
    }

    if (!this.showTradeMarkers || this._currentFills.length === 0) {
      this._fillGroupsByTime.clear();
      this._markersApi.setMarkers([]);
      this._hideFillTooltip();
      return;
    }

    const { markers, groupsByTime } = this._buildFillMarkers(this._currentFills);
    this._fillGroupsByTime = groupsByTime;
    this._markersApi.setMarkers(markers);

    if (markers.length === 0) {
      this._hideFillTooltip();
    }
  }

  private _buildFillMarkers(fills: FillEvent[]): { markers: SeriesMarker<Time>[]; groupsByTime: Map<number, ConsolidatedFillGroup[]> } {
    const normalizedAsset = this._normalizeAsset(this.selectedAsset);
    const groupsByTime = new Map<number, ConsolidatedFillGroup[]>();
    const markers: SeriesMarker<Time>[] = [];
    const range = this._getLoadedRange();

    if (!range) {
      return { markers, groupsByTime };
    }

    const groupedFills = new Map<string, ConsolidatedFillGroup>();

    for (const fill of fills) {
      if (this._normalizeAsset(fill.asset) !== normalizedAsset) {
        continue;
      }

      const fillTime = this._toCandleTimestamp(fill.timestamp);
      if (fillTime < range.start || fillTime > range.end) {
        continue;
      }

      const groupKey = `${fillTime}:${fill.side.toUpperCase()}`;
      const existingGroup = groupedFills.get(groupKey);

      if (existingGroup) {
        existingGroup.fills.push(fill);
        continue;
      }

      groupedFills.set(groupKey, {
        time: fillTime,
        side: fill.side,
        fills: [fill],
        totalSize: 0,
        totalFee: 0,
        totalClosedPnl: 0,
        averagePrice: 0
      });
    }

    for (const group of groupedFills.values()) {
      const isBuy = group.side === "Buy";
      const aggregatedGroup = this._aggregateFillGroup(group);
      const candleGroups = groupsByTime.get(aggregatedGroup.time) ?? [];
      candleGroups.push(aggregatedGroup);
      groupsByTime.set(aggregatedGroup.time, candleGroups);

      markers.push({
        time: aggregatedGroup.time as UTCTimestamp,
        position: isBuy ? "belowBar" : "aboveBar",
        color: isBuy ? PRICE_CHART_THEME.profit : PRICE_CHART_THEME.warning,
        shape: isBuy ? "arrowUp" : "arrowDown",
        text: this._buildMarkerText(aggregatedGroup)
      });
    }

    return {
      markers: markers.sort((left, right) => (left.time as number) - (right.time as number)),
      groupsByTime
    };
  }

  private _subscribeCrosshairMove(): void {
    if (!this._chart) {
      return;
    }

    this._crosshairHandler = (param: MouseEventParams<Time>) => {
      if (!this.showTradeMarkers || !param.time || !param.point) {
        this._hideFillTooltip();
        return;
      }

      const container = this._chartContainer.nativeElement;
      if (
        param.point.x < 0 ||
        param.point.y < 0 ||
        param.point.x > container.clientWidth ||
        param.point.y > container.clientHeight
      ) {
        this._hideFillTooltip();
        return;
      }

      const groups = this._fillGroupsByTime.get(param.time as number);
      if (!groups || groups.length === 0) {
        this._hideFillTooltip();
        return;
      }

      this._showFillTooltip(groups, param.point.x, param.point.y);
    };

    this._chart.subscribeCrosshairMove(this._crosshairHandler);
  }

  private _subscribeClick(): void {
    if (!this._chart) {
      return;
    }

    this._clickHandler = (param: MouseEventParams<Time>) => {
      if (!param.time) {
        return;
      }

      const timestamp = this._toUtcIso(param.time as number);
      if (this._candles.some(candle => this._toUtcIso(candle.time as number) === timestamp)) {
        this.selectedCandleOpenTimeUtc = timestamp;
      }
    };
    this._chart.subscribeClick(this._clickHandler);
  }

  private _showFillTooltip(groups: ConsolidatedFillGroup[], x: number, y: number): void {
    const container = this._chartContainer.nativeElement;
    const estimatedTooltipWidth = 260;
    const estimatedTooltipHeight = Math.max(
      96,
      groups.reduce((total: number, group: ConsolidatedFillGroup) => total + 132 + (group.fills.length * 24), 0)
    );

    this.tooltipGroups = groups;
    this.tooltipLeft = Math.max(12, Math.min(x + 16, container.clientWidth - estimatedTooltipWidth));
    this.tooltipTop = Math.max(12, Math.min(y - 16, container.clientHeight - estimatedTooltipHeight));
    this.tooltipVisible = true;
  }

  private _hideFillTooltip(): void {
    this.tooltipVisible = false;
    this.tooltipGroups = [];
  }

  private _aggregateFillGroup(group: ConsolidatedFillGroup): ConsolidatedFillGroup {
    const totalSize = group.fills.reduce((sum: number, fill: FillEvent) => sum + fill.size, 0);
    const totalFee = group.fills.reduce((sum: number, fill: FillEvent) => sum + fill.fee, 0);
    const totalClosedPnl = group.fills.reduce((sum: number, fill: FillEvent) => sum + fill.closedPnl, 0);
    const weightedPriceTotal = group.fills.reduce((sum: number, fill: FillEvent) => sum + (fill.price * fill.size), 0);

    return {
      ...group,
      fills: [...group.fills].sort((left: FillEvent, right: FillEvent) => new Date(left.timestamp).getTime() - new Date(right.timestamp).getTime()),
      totalSize,
      totalFee,
      totalClosedPnl,
      averagePrice: totalSize > 0 ? weightedPriceTotal / totalSize : 0
    };
  }

  private _buildMarkerText(group: ConsolidatedFillGroup): string {
    const countSuffix = group.fills.length > 1 ? ` (${group.fills.length})` : "";
    return `${group.side} ${group.totalSize.toFixed(4)} @ ${group.averagePrice.toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    })}${countSuffix}`;
  }

  private _getLoadedRange(): { start: number; end: number } | null {
    if (this._candles.length === 0 && !this._liveCandle) {
      return null;
    }

    const start = this._candles.length > 0
      ? (this._candles[0].time as number)
      : (this._liveCandle?.time as number);
    const endCandidates = [
      this._candles.length > 0 ? (this._candles[this._candles.length - 1].time as number) : null,
      this._liveCandle ? (this._liveCandle.time as number) : null
    ].filter((value): value is number => value !== null);

    return start == null || endCandidates.length === 0
      ? null
      : { start, end: Math.max(...endCandidates) };
  }

  private _toCandleTimestamp(value: string): number {
    const timeSeconds = Math.floor(new Date(value).getTime() / 1000);
    return Math.floor(timeSeconds / this._candleSeconds) * this._candleSeconds;
  }

  private _normalizeAsset(asset: string): string {
    return asset.replace(/-PERP$/i, "").toUpperCase();
  }

  private _toUtcIso(timeSeconds: number): string {
    return new Date(timeSeconds * 1000).toISOString();
  }

  private _activeIndicators(): ChartIndicatorId[] {
    const active: ChartIndicatorId[] = [];
    if (this.indicatorToggles.emaFast) active.push("EMA20");
    if (this.indicatorToggles.emaSlow) active.push("EMA50");
    if (this.indicatorToggles.emaTrend) active.push("EMA200");
    if (this.indicatorToggles.bollinger) active.push("BOLLINGER20_2");
    if (this.indicatorToggles.rsi) active.push("RSI14");
    if (this.indicatorToggles.macd) active.push("MACD12_26_9");
    return active;
  }

  private _refreshIndicatorSeries(): void {
    this._setLineSeriesData(this._emaFastSeries, this.indicatorToggles.emaFast ? this._mapIndicatorLineData(indicators => indicators?.emaFast) : []);
    this._setLineSeriesData(this._emaSlowSeries, this.indicatorToggles.emaSlow ? this._mapIndicatorLineData(indicators => indicators?.emaSlow) : []);
    this._setLineSeriesData(this._emaTrendSeries, this.indicatorToggles.emaTrend ? this._mapIndicatorLineData(indicators => indicators?.emaTrend) : []);

    const showBollinger = this.indicatorToggles.bollinger;
    this._setLineSeriesData(this._bollingerUpperSeries, showBollinger ? this._mapIndicatorLineData(indicators => indicators?.bollingerUpper) : []);
    this._setLineSeriesData(this._bollingerMiddleSeries, showBollinger ? this._mapIndicatorLineData(indicators => indicators?.bollingerMiddle) : []);
    this._setLineSeriesData(this._bollingerLowerSeries, showBollinger ? this._mapIndicatorLineData(indicators => indicators?.bollingerLower) : []);

    this._setLineSeriesData(this._rsiSeries, this.indicatorToggles.rsi ? this._mapIndicatorLineData(indicators => indicators?.rsi) : []);
    this._setLineSeriesData(this._macdLineSeries, this.indicatorToggles.macd ? this._mapIndicatorLineData(indicators => indicators?.macdLine) : []);
    this._setLineSeriesData(this._macdSignalSeries, this.indicatorToggles.macd ? this._mapIndicatorLineData(indicators => indicators?.macdSignal) : []);
    this._setHistogramSeriesData(this._macdHistogramSeries, this.indicatorToggles.macd ? this._mapMacdHistogramData() : []);
  }

  private _mapIndicatorLineData(selector: (indicators: ChartIndicatorValues | null | undefined) => number | null | undefined): LineData<UTCTimestamp>[] {
    return this._indicatorCandles
      .filter((candle: Candle) => selector(candle.indicators) != null)
      .map((candle: Candle) => ({
        time: Math.floor(candle.timestamp / 1000) as UTCTimestamp,
        value: selector(candle.indicators) as number,
      }));
  }

  private _mapMacdHistogramData(): HistogramData<UTCTimestamp>[] {
    return this._indicatorCandles
      .filter((candle: Candle) => candle.indicators?.macdHistogram != null)
      .map((candle: Candle) => {
        const value = candle.indicators?.macdHistogram as number;
        return {
          time: Math.floor(candle.timestamp / 1000) as UTCTimestamp,
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