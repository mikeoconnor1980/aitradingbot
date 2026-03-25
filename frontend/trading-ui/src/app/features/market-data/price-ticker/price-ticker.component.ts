import { DecimalPipe } from "@angular/common";
import { Component, DestroyRef, Input, OnChanges, OnInit, SimpleChanges, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatCardModule } from "@angular/material/card";
import { MarketInfo } from "../../../core/models/market-info.model";
import { PriceUpdate } from "../../../core/models/price-update.model";
import { SignalRService } from "../../../core/services/signalr.service";

@Component({
  selector: "app-price-ticker",
  standalone: true,
  imports: [MatCardModule, DecimalPipe],
  templateUrl: "./price-ticker.component.html",
  styleUrl: "./price-ticker.component.scss"
})
export class PriceTickerComponent implements OnInit, OnChanges {
  private readonly _signalRService = inject(SignalRService);
  private readonly _destroyRef = inject(DestroyRef);

  @Input() public seedMarketInfo: MarketInfo | null = null;
  @Input() public selectedAsset: string = 'BTC-PERP';

  public priceUpdate: PriceUpdate | null = null;

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedAsset']) {
      this.priceUpdate = null;
    }
    if (changes['seedMarketInfo'] && this.seedMarketInfo && !this.priceUpdate) {
      this.priceUpdate = {
        asset: this.seedMarketInfo.asset,
        lastPrice: this.seedMarketInfo.midPrice,
        high24h: this.seedMarketInfo.midPrice,
        low24h: this.seedMarketInfo.midPrice,
        volume24h: this.seedMarketInfo.volume24h,
        timestamp: Date.now(),
      };
    }
  }

  public ngOnInit(): void {
    this._signalRService.priceUpdate$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((update: PriceUpdate) => {
        if (update.asset === this.selectedAsset) {
          this.priceUpdate = update;
        }
      });
  }
}
