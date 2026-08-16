import { CommonModule } from "@angular/common";
import { Component, DestroyRef, OnInit, ViewChild, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSelectModule } from "@angular/material/select";
import { MatTableModule } from "@angular/material/table";
import { ActivatedRoute } from "@angular/router";
import { BehaviorSubject, Subject, interval, merge, of, EMPTY } from "rxjs";
import { catchError, startWith, switchMap } from "rxjs/operators";
import { Candle } from "../../core/models/candle.model";
import { FillEvent } from "../../core/models/fill-event.model";
import { MarketInfo } from "../../core/models/market-info.model";
import { TradableAsset } from "../../core/models/tradable-asset.model";
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { MarketDataService } from "../../core/services/market-data.service";
import { OrderService } from "../../core/services/order.service";
import { SignalRService } from "../../core/services/signalr.service";
import { ExchangeContextService } from "../../core/services/exchange-context.service";
import { AnalystChartContextService } from "../../core/services/analyst-chart-context.service";
import { AnalystSessionService } from "../../core/services/analyst-session.service";
import { RightPanelService } from "../../core/services/right-panel.service";
import { PriceChartComponent } from "./price-chart/price-chart.component";
import { PriceTickerComponent } from "./price-ticker/price-ticker.component";

@Component({
  selector: "app-market-data",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    PriceTickerComponent,
    PriceChartComponent
  ],
  templateUrl: "./market-data.component.html",
  styleUrl: "./market-data.component.scss"
})
export class MarketDataComponent implements OnInit {
  private static readonly POLL_INTERVAL_MS = 10_000;

  private readonly _destroyRef = inject(DestroyRef);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _orderService = inject(OrderService);
  private readonly _signalRService = inject(SignalRService);
  private readonly _exchangeContext = inject(ExchangeContextService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _analystChartContext = inject(AnalystChartContextService);
  private readonly _analystSession = inject(AnalystSessionService);
  private readonly _rightPanels = inject(RightPanelService);
  private readonly _selectedAsset$ = new BehaviorSubject<string>("BTC-PERP");
  private readonly _manualRefresh$ = new Subject<void>();
  private readonly _candleTrigger$ = new Subject<void>();

  public assets: TradableAsset[] = [{ symbol: "BTC-PERP", name: "Bitcoin", maxLeverage: 40, szDecimals: 5 }];
  public readonly timeframes: string[] = ["5m", "15m", "1h", "4h"];
  public readonly candleColumns: string[] = ["timestamp", "open", "high", "low", "close", "volume"];

  @ViewChild(PriceChartComponent) private readonly _priceChart?: PriceChartComponent;

  public selectedAsset = "BTC-PERP";
  public selectedTimeframe = "15m";
  public showFills = true;
  public marketInfo: MarketInfo | null = null;
  public candles: Candle[] = [];
  public fills: FillEvent[] = [];
  public marketInfoError: string | null = null;
  public candleError: string | null = null;
  public isLoadingMarketInfo = true;
  public isLoadingCandles = true;

  public ngOnInit(): void {
    this._startMarketInfoPolling();
    this._startCandleLoading();
    this._subscribeToFillEvents();
    this._candleTrigger$.next();
    this._loadFillsForAsset(this.selectedAsset);

    this._exchangeContext.exchange$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._loadAssets();
      });

    this._loadAssets();
  }

  public onAssetChanged(asset: string): void {
    this.selectedAsset = asset;
    this.marketInfo = null;
    this.candles = [];
    this.fills = [];
    this.marketInfoError = null;
    this.candleError = null;
    this._selectedAsset$.next(asset);
    this._candleTrigger$.next();
    this._loadFillsForAsset(asset);
  }

  public onTimeframeChanged(timeframe: string): void {
    this.selectedTimeframe = timeframe;
    this._candleTrigger$.next();
  }

  public onManualRefresh(): void {
    this._manualRefresh$.next();
    this._candleTrigger$.next();
    this._loadFillsForAsset(this.selectedAsset);
  }

  public onLoadMoreCandles(endTimeMs: number): void {
    this._marketDataService.getHistoricalCandles(this.selectedAsset, this.selectedTimeframe, endTimeMs).pipe(
      catchError(() => of([] as Candle[])),
      switchMap((candles: Candle[]) => {
        if (candles.length > 0) {
          return of(candles);
        }

        return this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe, endTimeMs).pipe(
          catchError(() => of([] as Candle[]))
        );
      })
    ).subscribe((candles: Candle[]) => {
      this._priceChart?.prependCandles(candles);
    });
  }

  public onToggleFills(): void {
    this.showFills = !this.showFills;
  }

  public askAnalyst(): void {
    this._analystChartContext.register(() => this._priceChart?.captureAnalystContext() ?? null);
    const chart = this._analystChartContext.captureCurrent();
    this._analystSession.start(chart ? { intent: "AnalyseChart", chart } : undefined);
    this._rightPanels.open("analyst");
  }

  private _startMarketInfoPolling(): void {
    this._selectedAsset$
      .pipe(
        switchMap((asset: string) =>
          merge(interval(MarketDataComponent.POLL_INTERVAL_MS).pipe(startWith(0)), this._manualRefresh$).pipe(
            switchMap(() => {
              this.isLoadingMarketInfo = true;
              return this._marketDataService.getMarketInfo(asset).pipe(
                catchError(() => {
                  this.marketInfo = null;
                  this.marketInfoError = "Failed to load market data. Will retry on next poll cycle.";
                  this.isLoadingMarketInfo = false;
                  return of<MarketInfo | null>(null);
                })
              );
            })
          )
        ),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((data: MarketInfo | null) => {
        if (!data) {
          return;
        }

        this.marketInfo = data;
        this.marketInfoError = null;
        this.isLoadingMarketInfo = false;
      });
  }

  private _startCandleLoading(): void {
    this._candleTrigger$
      .pipe(
        switchMap(() => {
          this.isLoadingCandles = true;
          this.candleError = null;
          return this._marketDataService.getHistoricalCandles(this.selectedAsset, this.selectedTimeframe).pipe(
            catchError(() => of([] as Candle[])),
            switchMap((candles: Candle[]) => {
              if (candles.length > 0) {
                return of(candles);
              }

              return this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe).pipe(
                catchError(() => {
                  this.candles = [];
                  this.candleError = "Failed to load candle data.";
                  this.isLoadingCandles = false;
                  return EMPTY;
                })
              );
            })
          );
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((data: Candle[]) => {
        this.candles = [...data].sort((a: Candle, b: Candle) => b.timestamp - a.timestamp);
        this.isLoadingCandles = false;
      });
  }

  private _subscribeToFillEvents(): void {
    this._signalRService.fillEvent$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((fill: FillEvent) => {
        if (this._toCoin(fill.asset) !== this._toCoin(this.selectedAsset)) {
          return;
        }

        if (this._hasFill(fill)) {
          return;
        }

        this.fills = [...this.fills, fill];
        this._priceChart?.addFill(fill);
      });
  }

  private _loadFillsForAsset(asset: string): void {
    this._apiService.getRecentFills(asset).subscribe((fills: FillEvent[]) => {
      this.fills = fills
        .filter((fill: FillEvent) => this._toCoin(fill.asset) === this._toCoin(asset))
        .sort((left: FillEvent, right: FillEvent) => new Date(left.timestamp).getTime() - new Date(right.timestamp).getTime());
    });
  }

  private _hasFill(candidate: FillEvent): boolean {
    return this.fills.some((fill: FillEvent) =>
      fill.orderId === candidate.orderId &&
      fill.timestamp === candidate.timestamp &&
      fill.side === candidate.side &&
      fill.price === candidate.price &&
      fill.size === candidate.size
    );
  }

  private _toCoin(asset: string): string {
    return asset.replace(/-PERP$/i, "").toUpperCase();
  }

  private _loadAssets(): void {
    this._orderService.getAvailableAssets().subscribe({
      next: (assets) => {
        this.assets = assets;
        const requestedSymbol = this._route.snapshot.queryParamMap.get("symbol");
        if (requestedSymbol && assets.some((asset) => asset.symbol === requestedSymbol)) {
          this.onAssetChanged(requestedSymbol);
          return;
        }
        if (!assets.some((asset) => asset.symbol === this.selectedAsset) && assets.length > 0) {
          this.onAssetChanged(assets[0].symbol);
        }
      }
    });
  }
}