import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatChipsModule } from "@angular/material/chips";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { HttpContext } from "@angular/common/http";
import { interval, of, switchMap, catchError, startWith } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { MarketContextService } from "../../../core/services/market-context.service";
import { LlmContextDto } from "../../../core/models/llm-context.model";
import { LayoutService } from "../../../core/services/layout.service";

@Component({
  selector: "app-market-context-card",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: "./market-context-card.component.html",
  styleUrls: ["./market-context-card.component.scss"],
})
export class MarketContextCardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _contextService = inject(MarketContextService);
  private readonly _layout = inject(LayoutService);
  private readonly _silentContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public context: LlmContextDto | null = null;
  public isLoading = true;
  public isStale = false;
  public symbol = "BTC";
  public readonly isMobile = this._layout.isMobile;

  public ngOnInit(): void {
    interval(60_000)
      .pipe(
        startWith(0),
        switchMap(() =>
          this._contextService.getCurrentContext(this.symbol, this._silentContext).pipe(
            catchError(() => of(null))
          )
        ),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((result) => {
        this.isLoading = false;
        if (result) {
          this.context = result;
          this._checkStaleness();
        }
      });
  }

  public get regimeIcon(): string {
    switch (this.context?.derivedRegime) {
      case "Aggressive": return "rocket_launch";
      case "Normal": return "balance";
      case "Defensive": return "shield";
      case "RiskOff": return "block";
      default: return "help_outline";
    }
  }

  public get regimeColor(): string {
    switch (this.context?.derivedRegime) {
      case "Aggressive": return "primary";
      case "Normal": return "";
      case "Defensive": return "warn";
      case "RiskOff": return "warn";
      default: return "";
    }
  }

  public get sentimentIcon(): string {
    switch (this.context?.marketSentiment) {
      case "Bullish": return "trending_up";
      case "Bearish": return "trending_down";
      default: return "trending_flat";
    }
  }

  public get eventRiskClass(): string {
    switch (this.context?.eventRisk) {
      case "High": return "market-context__risk--high";
      case "Medium": return "market-context__risk--medium";
      case "Low": return "market-context__risk--low";
      default: return "";
    }
  }

  public get confidencePercent(): number {
    return Math.round((this.context?.confidence ?? 0) * 100);
  }

  public get conclusion(): string {
    if (!this.context) {
      return "Market context is unavailable";
    }

    return `${this.context.derivedRegime} regime · ${this.context.marketSentiment} sentiment`;
  }

  public get lastUpdatedText(): string {
    if (!this.context) {
      return "";
    }
    const now = Date.now();
    const diffMinutes = Math.round((now - this.context.generatedAtUtc) / 60_000);

    if (diffMinutes < 1) {
      return "just now";
    }
    if (diffMinutes < 60) {
      return `${diffMinutes}m ago`;
    }
    const diffHours = Math.round(diffMinutes / 60);
    return `${diffHours}h ago`;
  }

  private _checkStaleness(): void {
    if (!this.context) {
      this.isStale = false;
      return;
    }
    const diffMs = Date.now() - this.context.generatedAtUtc;
    this.isStale = diffMs > 2 * 60 * 60 * 1000; // stale if > 2 hours old
  }
}
