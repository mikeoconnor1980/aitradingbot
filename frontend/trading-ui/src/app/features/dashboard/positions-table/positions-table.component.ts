import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Position } from "../../../core/models/position.model";
import { FundingIndicatorComponent } from "./funding-indicator/funding-indicator.component";

type SortableColumn = "asset" | "size" | "unrealisedPnl" | "entryPrice" | "markPrice";
type SortDirection = "asc" | "desc" | null;

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatTooltipModule, FundingIndicatorComponent],
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
  public readonly expandedPositionKeys = new Set<string>();
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

  public toggleDetails(position: Position): void {
    const key = this.getPositionKey(position);

    if (this.expandedPositionKeys.has(key)) {
      this.expandedPositionKeys.delete(key);
      return;
    }

    this.expandedPositionKeys.add(key);
  }

  public isDetailsExpanded(position: Position): boolean {
    return this.expandedPositionKeys.has(this.getPositionKey(position));
  }

  public clearFilter(): void {
    this.filterText = "";
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "pnl--profit" : "pnl--loss";
  }

  public getMarkPriceClass(position: Position): string {
    if (!this.hasMarkPrice(position) || position.markPrice === position.entryPrice) {
      return "";
    }

    const favorable = position.side === "Long"
      ? position.markPrice > position.entryPrice
      : position.markPrice < position.entryPrice;

    return favorable ? "mark-price--favorable" : "mark-price--adverse";
  }

  public hasMarkPrice(position: Position): boolean {
    return position.markPrice > 0;
  }

  public getMarkPriceIcon(position: Position): string {
    const cssClass = this.getMarkPriceClass(position);
    if (!cssClass) {
      return "";
    }

    return cssClass === "mark-price--favorable" ? "arrow_upward" : "arrow_downward";
  }

  public getNotional(position: Position): number {
    return Math.abs(position.size) * position.markPrice;
  }

  public getNotionalLabel(position: Position): string {
    if (!this.hasMarkPrice(position)) {
      return "—";
    }

    return `$${this.getNotional(position).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  public getMarginTooltip(position: Position): string {
    const margin = position.marginUsed;
    const pct = this.equity > 0 ? (margin / this.equity) * 100 : 0;
    return `Margin: $${margin.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${pct.toFixed(1)}% of equity)`;
  }

  public hasMarginUsed(position: Position): boolean {
    return position.marginUsed > 0;
  }

  public getMarginLabel(position: Position): string {
    if (!this.hasMarginUsed(position)) {
      return "—";
    }

    return `$${position.marginUsed.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  public getMarginPercentLabel(position: Position): string {
    if (!this.hasMarginUsed(position) || this.equity <= 0) {
      return "—";
    }

    return `${((position.marginUsed / this.equity) * 100).toFixed(1)}% of equity`;
  }

  public getLiquidationLabel(position: Position): string {
    if (position.liquidationPrice <= 0) {
      return "—";
    }

    return position.liquidationPrice.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}