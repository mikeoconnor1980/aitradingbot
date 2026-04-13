import { Injectable } from "@angular/core";
import { BehaviorSubject } from "rxjs";
import { StrategyConfig } from "../models/strategy.model";

const SESSION_KEY = "strategy_draft";

@Injectable({ providedIn: "root" })
export class StrategyDraftService {
  private readonly _draft$ = new BehaviorSubject<StrategyConfig | null>(this._loadFromSession());

  public readonly draft$ = this._draft$.asObservable();

  public get draft(): StrategyConfig | null {
    return this._draft$.value;
  }

  public save(config: StrategyConfig): void {
    this._draft$.next(config);
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(config));
  }

  public clear(): void {
    this._draft$.next(null);
    sessionStorage.removeItem(SESSION_KEY);
  }

  public hasDraft(): boolean {
    return this._draft$.value !== null;
  }

  private _loadFromSession(): StrategyConfig | null {
    const raw = sessionStorage.getItem(SESSION_KEY);

    if (raw === null) {
      return null;
    }

    try {
      return JSON.parse(raw) as StrategyConfig;
    } catch {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
  }
}
