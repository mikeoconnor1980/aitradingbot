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
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  CandlestickData,
  CandlestickSeries,
  createChart,
  CrosshairMode,
  IChartApi,
  ISeriesApi,
  LogicalRange,
  UTCTimestamp
} from "lightweight-charts";
import { PriceUpdate } from "../../../core/models/price-update.model";
import { Candle } from "../../../core/models/candle.model";
import { SignalRService } from "../../../core/services/signalr.service";

@Component({
  selector: "app-price-chart",
  standalone: true,
  imports: [],
  templateUrl: "./price-chart.component.html",
  styleUrl: "./price-chart.component.scss"
})
export class PriceChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  private static readonly TIMEFRAME_SECONDS: Record<string, number> = {
    '1m': 60, '3m': 180, '5m': 300, '15m': 900, '30m': 1800,
    '1h': 3600, '4h': 14400, '1d': 86400
  };

  @Input() public seedCandles: Candle[] = [];
  @Input() public selectedAsset = "BTC-PERP";
  @Input() public selectedTimeframe = "15m";

  @Output() public loadMoreCandles = new EventEmitter<number>();

  public get timeWindowLabel(): string {
    if (!this._candles.length) return '';
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

  private readonly _signalRService = inject(SignalRService);
  private readonly _destroyRef = inject(DestroyRef);

  private _chart: IChartApi | null = null;
  private _candleSeries: ISeriesApi<"Candlestick"> | null = null;
  private _resizeObserver: ResizeObserver | null = null;
  private _candles: CandlestickData<UTCTimestamp>[] = [];
  private _liveCandle: CandlestickData<UTCTimestamp> | null = null;
  private _isLoadingHistory = false;
  private _oldestTimestamp: number | null = null;

  public ngAfterViewInit(): void {
    this._initChart();
    this._seedFromCandles();
    this._subscribeToUpdates();
    this._subscribeToVisibleRangeChange();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if ((changes['seedCandles'] || changes['selectedTimeframe'] || changes['selectedAsset']) && this._candleSeries) {
      this._liveCandle = null;
      this._isLoadingHistory = false;
      this._oldestTimestamp = null;
      this._seedFromCandles();
    }
  }

  public ngOnDestroy(): void {
    this._resizeObserver?.disconnect();
    this._chart?.remove();
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
      this._candles = [...unique, ...this._candles];
      this._oldestTimestamp = this._candles[0].time as number;
      this._candleSeries?.setData(this._candles);
    }

    this._isLoadingHistory = false;
  }

  private _initChart(): void {
    const container = this._chartContainer.nativeElement;

    this._chart = createChart(container, {
      width: container.clientWidth,
      height: 300,
      layout: {
        background: { color: "#1a1a2e" },
        textColor: "#a0a0b0"
      },
      grid: {
        vertLines: { color: "#2a2a3e" },
        horzLines: { color: "#2a2a3e" }
      },
      timeScale: {
        timeVisible: true,
        secondsVisible: false,
        borderColor: "#2a2a3e"
      },
      rightPriceScale: {
        borderColor: "#2a2a3e"
      },
      crosshair: {
        mode: CrosshairMode.Normal
      }
    });

    this._candleSeries = this._chart.addSeries(CandlestickSeries, {
      upColor: "#26a69a",
      downColor: "#ef5350",
      borderVisible: false,
      wickUpColor: "#26a69a",
      wickDownColor: "#ef5350",
      priceFormat: {
        type: "price",
        precision: 2,
        minMove: 0.01
      }
    });

    this._resizeObserver = new ResizeObserver((entries: ResizeObserverEntry[]) => {
      for (const entry of entries) {
        const { width } = entry.contentRect;
        this._chart?.applyOptions({ width });
      }
    });

    this._resizeObserver.observe(container);
  }

  private _seedFromCandles(): void {
    if (!this.seedCandles.length) return;

    const sorted = [...this.seedCandles].sort((a, b) => a.timestamp - b.timestamp);
    this._candles = sorted.map((c) => ({
      time: Math.floor(c.timestamp / 1000) as UTCTimestamp,
      open: c.open,
      high: c.high,
      low: c.low,
      close: c.close,
    }));
    this._oldestTimestamp = this._candles.length ? (this._candles[0].time as number) : null;
    this._candleSeries?.setData(this._candles);
    this._chart?.timeScale().fitContent();
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
      }
      this._liveCandle = { time: candleTime, open: price, high: price, low: price, close: price };
      this._chart?.timeScale().scrollToRealTime();
    }

    if (this._liveCandle) {
      this._candleSeries?.update(this._liveCandle);
    }
  }
}