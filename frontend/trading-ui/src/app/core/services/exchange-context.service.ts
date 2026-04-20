import { Injectable } from "@angular/core";
import { BehaviorSubject, Observable } from "rxjs";

export type SupportedExchange = "Hyperliquid" | "Binance";

@Injectable({ providedIn: "root" })
export class ExchangeContextService {
  private readonly _exchange$ = new BehaviorSubject<SupportedExchange>("Hyperliquid");

  public readonly exchange$: Observable<SupportedExchange> = this._exchange$.asObservable();

  public get exchange(): SupportedExchange {
    return this._exchange$.value;
  }

  public setExchange(exchange: string | null | undefined): void {
    this._exchange$.next(this.normalize(exchange));
  }

  private normalize(exchange: string | null | undefined): SupportedExchange {
    return exchange?.trim().toLowerCase() === "binance" ? "Binance" : "Hyperliquid";
  }
}