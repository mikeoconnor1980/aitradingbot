import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Position } from "../../../core/models/position.model";

type SortableColumn = "asset" | "size" | "unrealisedPnl" | "entryPrice" | "markPrice" | "liquidationPrice";
type SortDirection = "asc" | "desc" | null;

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatTooltipModule],
  templateUrl: "./positions-table.component.html",
  styleUrl: "./positions-table.component.scss"
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  @Input()
  public equity = 0;

  @Output()
  public closePosition = new EventEmitter<Position>();

  @Output()
  public closeAllPositions = new EventEmitter<void>();

  public readonly loadingPositionKeys = new Set<string>();
  public globalLoading = false;
  public sortColumn: SortableColumn | null = null;
  public sortDirection: SortDirection = null;
  public filterText = "";

  public get sortedFilteredPositions(): Position[] {
    let result = this.positions;

    if (this.filterText) {
      const term = this.filterText.toLowerCase();
      result = result.filter((position) => position.asset.toLowerCase().includes(term));
    }

    if (!this.sortColumn || !this.sortDirection) {
      return result;
    }

    const column = this.sortColumn;
    const multiplier = this.sortDirection === "asc" ? 1 : -1;

    return [...result].sort((a, b) => {
      if (column === "asset") {
        return a.asset.localeCompare(b.asset) * multiplier;
      }

      return ((a[column] as number) - (b[column] as number)) * multiplier;
    });
  }

  public get isFiltered(): boolean {
    return this.filterText.length > 0;
  }

  public get filteredCount(): number {
    return this.sortedFilteredPositions.length;
  }

  public getPositionKey(position: Position): string {
    return position.asset + position.side;
  }

  public isLoading(position: Position): boolean {
    return this.globalLoading || this.loadingPositionKeys.has(this.getPositionKey(position));
  }

  public setLoading(key: string, loading: boolean): void {
    if (loading) {
      this.loadingPositionKeys.add(key);
      return;
    }

    this.loadingPositionKeys.delete(key);
  }

  public onCloseClick(position: Position): void {
    if (this.isLoading(position)) {
      return;
    }

    this.closePosition.emit(position);
  }

  public setGlobalLoading(loading: boolean): void {
    this.globalLoading = loading;
  }

  public onSort(column: SortableColumn): void {
    if (this.sortColumn !== column) {
      this.sortColumn = column;
      this.sortDirection = "desc";
      return;
    }

    if (this.sortDirection === "desc") {
      this.sortDirection = "asc";
      return;
    }

    if (this.sortDirection === "asc") {
      this.sortColumn = null;
      this.sortDirection = null;
    }
  }

  public onFilterChange(event: Event): void {
    this.filterText = (event.target as HTMLInputElement).value;
  }

  public clearFilter(): void {
    this.filterText = "";
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "pnl--profit" : "pnl--loss";
  }

  public getMarkPriceClass(position: Position): string {
    const favorable = position.side === "Long"
      ? position.markPrice >= position.entryPrice
      : position.markPrice <= position.entryPrice;
    return favorable ? "mark-price--favorable" : "mark-price--adverse";
  }

  public getNotional(position: Position): number {
    return Math.abs(position.size) * position.markPrice;
  }

  public getNotionalTooltip(position: Position): string {
    const notional = this.getNotional(position);
    return `Notional: $${notional.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  public getMarginTooltip(position: Position, equity: number): string {
    const margin = position.marginUsed;
    const pct = equity > 0 ? (margin / equity) * 100 : 0;
    return `Margin: $${margin.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${pct.toFixed(1)}% of equity)`;
  }

  public getFundingClass(position: Position): string {
    const receiving = position.side === "Long"
      ? position.fundingRate > 0
      : position.fundingRate < 0;
    if (position.fundingRate === 0) {
      return "";
    }
    return receiving ? "funding--receiving" : "funding--paying";
  }

  public getFundingTooltip(position: Position): string {
    const hourlyRate = position.fundingRate * 100;
    const notional = this.getNotional(position);
    const dailyUsd = notional * Math.abs(position.fundingRate) * 24;
    const receiving = position.side === "Long"
      ? position.fundingRate > 0
      : position.fundingRate < 0;
    const sign = receiving ? "+" : "-";
    return `Hourly: ${hourlyRate.toFixed(4)}% | Est. daily: ${sign}$${dailyUsd.toFixed(2)}`;
  }
}