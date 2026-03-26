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
import { BehaviorSubject, Subject, interval, merge, of, EMPTY } from "rxjs";
import { catchError, startWith, switchMap } from "rxjs/operators";
import { Candle } from "../../core/models/candle.model";
import { MarketInfo } from "../../core/models/market-info.model";
import { TradableAsset } from "../../core/models/tradable-asset.model";
import { MarketDataService } from "../../core/services/market-data.service";
import { OrderService } from "../../core/services/order.service";
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
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _orderService = inject(OrderService);
  private readonly _selectedAsset$ = new BehaviorSubject<string>("BTC-PERP");
  private readonly _manualRefresh$ = new Subject<void>();
  private readonly _candleTrigger$ = new Subject<void>();

  public assets: TradableAsset[] = [{ symbol: "BTC-PERP", name: "Bitcoin", maxLeverage: 40, szDecimals: 5 }];
  public readonly timeframes: string[] = ["5m", "15m", "1h", "4h"];
  public readonly candleColumns: string[] = ["timestamp", "open", "high", "low", "close", "volume"];

  @ViewChild(PriceChartComponent) private readonly _priceChart?: PriceChartComponent;

  public selectedAsset = "BTC-PERP";
  public selectedTimeframe = "15m";
  public marketInfo: MarketInfo | null = null;
  public candles: Candle[] = [];
  public marketInfoError: string | null = null;
  public candleError: string | null = null;
  public isLoadingMarketInfo = true;
  public isLoadingCandles = true;

  public ngOnInit(): void {
    this._startMarketInfoPolling();
    this._startCandleLoading();
    this._candleTrigger$.next();

    this._orderService.getAvailableAssets().subscribe({
      next: (assets) => {
        this.assets = assets;
      }
    });
  }

  public onAssetChanged(asset: string): void {
    this.selectedAsset = asset;
    this.marketInfo = null;
    this.candles = [];
    this.marketInfoError = null;
    this.candleError = null;
    this._selectedAsset$.next(asset);
    this._candleTrigger$.next();
  }

  public onTimeframeChanged(timeframe: string): void {
    this.selectedTimeframe = timeframe;
    this._candleTrigger$.next();
  }

  public onManualRefresh(): void {
    this._manualRefresh$.next();
    this._candleTrigger$.next();
  }

  public onLoadMoreCandles(endTimeMs: number): void {
    this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe, endTimeMs).subscribe({
      next: (candles) => this._priceChart?.prependCandles(candles),
      error: () => this._priceChart?.prependCandles([]),
    });
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
          return this._marketDataService.getCandles(this.selectedAsset, this.selectedTimeframe).pipe(
            catchError(() => {
              this.candles = [];
              this.candleError = "Failed to load candle data.";
              this.isLoadingCandles = false;
              return EMPTY;
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
}