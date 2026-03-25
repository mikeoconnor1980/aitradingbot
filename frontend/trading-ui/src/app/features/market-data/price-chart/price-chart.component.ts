import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
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
  private static readonly ROLLING_WINDOW_SECONDS = 24 * 60 * 60;

  private static readonly TIMEFRAME_SECONDS: Record<string, number> = {
    '1m': 60, '3m': 180, '5m': 300, '15m': 900, '30m': 1800,
    '1h': 3600, '4h': 14400, '1d': 86400
  };

  @Input() public seedCandles: Candle[] = [];
  @Input() public selectedAsset: string = 'BTC-PERP';
  @Input() public selectedTimeframe: string = '15m';

  public get timeWindowLabel(): string {
    if (!this.seedCandles.length) return '';
    const timestamps = this.seedCandles.map(c => c.timestamp);
    const diffMs = Math.max(...timestamps) - Math.min(...timestamps);
    const diffHours = Math.round(diffMs / (1000 * 60 * 60));
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
  // Forming live candle — built up from 500ms ticks
  private _liveCandle: CandlestickData<UTCTimestamp> | null = null;

  public ngAfterViewInit(): void {
    this._initChart();
    this._seedFromCandles();
    this._subscribeToUpdates();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if ((changes['seedCandles'] || changes['selectedTimeframe'] || changes['selectedAsset']) && this._candleSeries) {
      this._liveCandle = null;
      this._seedFromCandles();
    }
  }

  public ngOnDestroy(): void {
    this._resizeObserver?.disconnect();
    this._chart?.remove();
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

  private _updateLiveCandle(update: PriceUpdate): void {
    if (update.asset !== this.selectedAsset) return;
    const timeSeconds = Math.floor(update.timestamp / 1000);
    const candleTime = (Math.floor(timeSeconds / this._candleSeconds) * this._candleSeconds) as UTCTimestamp;
    const price = update.lastPrice;

    if (this._liveCandle && this._liveCandle.time === candleTime) {
      // Update the forming candle in-place
      this._liveCandle.high = Math.max(this._liveCandle.high, price);
      this._liveCandle.low = Math.min(this._liveCandle.low, price);
      this._liveCandle.close = price;
    } else {
      // New candle boundary — commit previous live candle and start fresh
      if (this._liveCandle) {
        this._candles.push({ ...this._liveCandle });
        // Trim candles outside rolling window
        const cutoff = (candleTime - PriceChartComponent.ROLLING_WINDOW_SECONDS) as UTCTimestamp;
        this._candles = this._candles.filter(c => c.time >= cutoff);
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