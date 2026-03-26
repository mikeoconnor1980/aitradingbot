import { Component, DestroyRef, inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatIconModule } from "@angular/material/icon";
import { AccountStateService } from "../../../core/services/account-state.service";
import { HyperliquidApiService } from "../../../core/services/hyperliquid-api.service";
import { UserEvent } from "../../../core/models/user-event.model";
import { FillEvent } from "../../../core/models/fill-event.model";
import { OrderUpdate } from "../../../core/models/order-update.model";

interface ActivityFeedFillItem {
  timestamp: Date;
  side: string;
  size: number;
  price: number;
  fee: number;
  closedPnl: number;
}

interface ActivityFeedRow {
  key: string;
  timestamp: Date;
  typeLabel: string;
  asset: string;
  description: string;
  realizedPnlLabel: string;
  isPositivePnl: boolean;
  isNegativePnl: boolean;
  badgeClasses: Record<string, boolean>;
  orderId: string;
  feeLabel: string;
  recordedLabel: string;
  itemizedFills: ActivityFeedFillItem[];
}

@Component({
  selector: "app-activity-feed",
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: "./activity-feed.component.html",
  styleUrls: ["./activity-feed.component.scss"]
})
export class ActivityFeedComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _accountState = inject(AccountStateService);
  private readonly _apiService = inject(HyperliquidApiService);
  private readonly _expandedEventKeys = new Set<string>();

  public events: UserEvent[] = [];
  public displayEvents: ActivityFeedRow[] = [];

  public ngOnInit(): void {
    this._accountState.events$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((events: UserEvent[]) => {
        this.events = events;
        this.displayEvents = this._buildDisplayEvents(events);
      });

    this._loadRecentFills();
  }

  private _loadRecentFills(): void {
    this._apiService.getRecentFills().subscribe({
      next: (fills) => {
        this._accountState.seedFillEvents(fills);
      },
      error: () => {
        // Silently fail — WebSocket events will still work
      }
    });
  }

  public toggleDetails(event: ActivityFeedRow): void {
    const key = event.key;

    if (this._expandedEventKeys.has(key)) {
      this._expandedEventKeys.delete(key);
      return;
    }

    this._expandedEventKeys.add(key);
  }

  public isDetailsExpanded(event: ActivityFeedRow): boolean {
    return this._expandedEventKeys.has(event.key);
  }

  public getFillSizeLabel(fill: ActivityFeedFillItem): string {
    return this._formatNumber(fill.size, 2, 5);
  }

  public getFillPriceLabel(fill: ActivityFeedFillItem): string {
    return this._formatNumber(fill.price, 0, 5);
  }

  public getItemizedRealizedPnlLabel(fill: ActivityFeedFillItem): string {
    if (!this._shouldDisplayRealizedPnl(fill.closedPnl, "")) {
      return "—";
    }

    return this._formatUsdSigned(fill.closedPnl);
  }

  public hasMultipleItems(event: ActivityFeedRow): boolean {
    return event.itemizedFills.length > 1;
  }

  private _getFillTypeLabel(fill: FillEvent): string {
    const direction = fill.direction.trim();

    if (direction.length > 0) {
      return direction;
    }

    return fill.side;
  }

  private _shouldDisplayRealizedPnl(closedPnl: number, direction: string): boolean {
    const normalizedDirection = direction.trim().toLowerCase();

    return normalizedDirection.startsWith("close") || normalizedDirection.startsWith("reduce") || closedPnl !== 0;
  }

  private _formatUsdSigned(value: number): string {
    const sign = value > 0 ? "+" : value < 0 ? "-" : "";
    return `$${sign}${Math.abs(value).toFixed(2)}`;
  }

  private _buildDisplayEvents(events: UserEvent[]): ActivityFeedRow[] {
    const rows: ActivityFeedRow[] = [];
    const fillGroups = new Map<string, ActivityFeedRow>();

    for (const event of events) {
      if (event.type === "Fill") {
        const fill = event.data as FillEvent;
        const typeLabel = this._getFillTypeLabel(fill);
        const groupKey = `Fill:${fill.orderId}:${fill.asset}:${typeLabel}`;
        const item: ActivityFeedFillItem = {
          timestamp: event.timestamp,
          side: fill.side,
          size: fill.size,
          price: fill.price,
          fee: fill.fee,
          closedPnl: fill.closedPnl
        };

        const existing = fillGroups.get(groupKey);
        if (existing) {
          existing.itemizedFills.push(item);
          existing.description = this._buildAggregatedFillDescription(existing.asset, existing.itemizedFills);
          existing.feeLabel = this._formatUsd(existing.itemizedFills.reduce((sum, current) => sum + current.fee, 0), 4);
          const realizedPnl = existing.itemizedFills.reduce((sum, current) => sum + current.closedPnl, 0);
          existing.realizedPnlLabel = this._shouldDisplayRealizedPnl(realizedPnl, typeLabel)
            ? this._formatUsdSigned(realizedPnl)
            : "—";
          existing.isPositivePnl = realizedPnl > 0;
          existing.isNegativePnl = realizedPnl < 0;
          continue;
        }

        const realizedPnlLabel = this._shouldDisplayRealizedPnl(fill.closedPnl, typeLabel)
          ? this._formatUsdSigned(fill.closedPnl)
          : "—";

        const row: ActivityFeedRow = {
          key: groupKey,
          timestamp: event.timestamp,
          typeLabel,
          asset: fill.asset,
          description: this._buildAggregatedFillDescription(fill.asset, [item]),
          realizedPnlLabel,
          isPositivePnl: fill.closedPnl > 0,
          isNegativePnl: fill.closedPnl < 0,
          badgeClasses: this._getFillBadgeClasses(typeLabel),
          orderId: fill.orderId,
          feeLabel: this._formatUsd(fill.fee, 4),
          recordedLabel: `${event.timestamp.getFullYear()}-${String(event.timestamp.getMonth() + 1).padStart(2, "0")}-${String(event.timestamp.getDate()).padStart(2, "0")} ${String(event.timestamp.getHours()).padStart(2, "0")}:${String(event.timestamp.getMinutes()).padStart(2, "0")}:${String(event.timestamp.getSeconds()).padStart(2, "0")} UTC`,
          itemizedFills: [item]
        };

        fillGroups.set(groupKey, row);
        rows.push(row);
        continue;
      }

      const order = event.data as OrderUpdate;
      rows.push({
        key: `OrderUpdate:${order.orderId}:${event.timestamp.getTime()}`,
        timestamp: event.timestamp,
        typeLabel: "Order Update",
        asset: order.asset,
        description: `${order.asset} — ${order.status} (filled: ${order.filledSize}, remaining: ${order.remainingSize})`,
        realizedPnlLabel: "—",
        isPositivePnl: false,
        isNegativePnl: false,
        badgeClasses: { "activity-feed__badge--order": true },
        orderId: order.orderId,
        feeLabel: "—",
        recordedLabel: `${event.timestamp.getFullYear()}-${String(event.timestamp.getMonth() + 1).padStart(2, "0")}-${String(event.timestamp.getDate()).padStart(2, "0")} ${String(event.timestamp.getHours()).padStart(2, "0")}:${String(event.timestamp.getMinutes()).padStart(2, "0")}:${String(event.timestamp.getSeconds()).padStart(2, "0")} UTC`,
        itemizedFills: []
      });
    }

    return rows;
  }

  private _getFillBadgeClasses(label: string): Record<string, boolean> {
    const normalizedLabel = label.toLowerCase();

    return {
      "activity-feed__badge--open-long": normalizedLabel === "open long",
      "activity-feed__badge--close-long": normalizedLabel === "close long",
      "activity-feed__badge--open-short": normalizedLabel === "open short",
      "activity-feed__badge--close-short": normalizedLabel === "close short",
      "activity-feed__badge--fill": !["open long", "close long", "open short", "close short"].includes(normalizedLabel)
    };
  }

  private _buildAggregatedFillDescription(asset: string, fills: ActivityFeedFillItem[]): string {
    const totalSize = fills.reduce((sum, current) => sum + current.size, 0);
    const side = fills[0]?.side ?? "";
    const allSamePrice = fills.every((fill) => fill.price === fills[0].price);
    const price = allSamePrice
      ? fills[0].price
      : fills.reduce((sum, current) => sum + (current.size * current.price), 0) / totalSize;
    const priceSuffix = allSamePrice ? "" : " avg";

    return `${side} ${this._formatNumber(totalSize, 2, 5)} ${asset} @ ${this._formatNumber(price, 0, 5)}${priceSuffix}`;
  }

  private _formatUsd(value: number, digits: number): string {
    return `$${value.toFixed(digits)}`;
  }

  private _formatNumber(value: number, minimumFractionDigits: number, maximumFractionDigits: number): string {
    return value.toLocaleString("en-US", {
      minimumFractionDigits,
      maximumFractionDigits,
      useGrouping: false
    });
  }
}
