import { DatePipe, DecimalPipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, Input, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatOptionModule } from "@angular/material/core";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatSelectChange, MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { finalize } from "rxjs";
import {
  BacktestDebugResponse,
  CandleEvaluation,
  OrderEvent,
  OrderEventType
} from "../../../core/models/backtest-debug.model";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../core/interceptors/http-context-tokens";
import { BacktestService } from "../../../core/services/backtest.service";

type SortableColumn =
  | "entryTime"
  | "exitTime"
  | "entryPrice"
  | "exitPrice"
  | "side"
  | "size"
  | "pnl"
  | "fees"
  | "initialRDollars"
  | "rMultipleResult"
  | "mfe"
  | "mae"
  | "exitReason";
type SortDirection = "asc" | "desc" | null;
type SetupDetectedFilter = "all" | "true" | "false";

@Component({
  selector: "app-trade-log-table",
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatOptionModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: "./trade-log-table.component.html",
  styleUrl: "./trade-log-table.component.scss"
})
export class TradeLogTableComponent {
  private readonly _backtestService = inject(BacktestService);
  private readonly _expandedTradeKeys = new Set<string>();
  private readonly _debugDataCache = new Map<string, BacktestDebugResponse | null>();
  private readonly _loadingCycleIds = new Set<string>();
  private readonly _debugErrors = new Map<string, string>();
  private readonly _signalTypeFilters = new Map<string, string>();
  private readonly _setupDetectedFilters = new Map<string, SetupDetectedFilter>();

  @Input()
  public trades: BacktestTrade[] = [];

  @Input()
  public backtestId = "";

  @Input()
  public hasAuditLog = false;

  public readonly orderEventTypes = OrderEventType;
  public sortColumn: SortableColumn | null = null;
  public sortDirection: SortDirection = null;

  public get hasAnyTrades(): boolean {
    return this.trades.length > 0;
  }

  public get hasRData(): boolean {
    return this.trades.some((trade: BacktestTrade) =>
      trade.initialRDollars !== null
      && trade.initialRDollars !== undefined
      || trade.rMultipleResult !== null
      && trade.rMultipleResult !== undefined
      || trade.mfe !== null
      && trade.mfe !== undefined
      || trade.mae !== null
      && trade.mae !== undefined);
  }

  public get closedTradeDetailsColspan(): number {
    return this.hasRData ? 14 : 10;
  }

  public get completedTrades(): BacktestTrade[] {
    return this._sortTrades(this.trades.filter((trade: BacktestTrade) => trade.exitTime !== null && trade.pnl !== null));
  }

  public get openTrades(): BacktestTrade[] {
    return this._sortTrades(this.trades.filter((trade: BacktestTrade) => trade.exitTime === null || trade.pnl === null));
  }

  public getTradeKey(trade: BacktestTrade): string {
    return `${trade.gridCycleId ?? "no-cycle"}:${trade.entryTime}:${trade.tradeType}:${trade.side}`;
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

  public getSortIcon(column: SortableColumn): string {
    if (this.sortColumn !== column) {
      return "unfold_more";
    }

    return this.sortDirection === "asc" ? "arrow_upward" : "arrow_downward";
  }

  public hasDebugData(trade: BacktestTrade): boolean {
    return this.hasAuditLog && this._getCycleId(trade) !== null;
  }

  public toggleDetails(trade: BacktestTrade): void {
    if (!this.hasDebugData(trade)) {
      return;
    }

    const tradeKey = this.getTradeKey(trade);
    if (this._expandedTradeKeys.has(tradeKey)) {
      this._expandedTradeKeys.delete(tradeKey);
      return;
    }

    this._expandedTradeKeys.add(tradeKey);

    const cycleId = this._getCycleId(trade);
    if (cycleId) {
      this._loadDebugData(cycleId);
    }
  }

  public isExpanded(trade: BacktestTrade): boolean {
    return this._expandedTradeKeys.has(this.getTradeKey(trade));
  }

  public isLoading(trade: BacktestTrade): boolean {
    const cycleId = this._getCycleId(trade);
    return cycleId !== null && this._loadingCycleIds.has(cycleId);
  }

  public getDebugData(trade: BacktestTrade): BacktestDebugResponse | null | undefined {
    const cycleId = this._getCycleId(trade);
    return cycleId === null ? null : this._debugDataCache.get(cycleId);
  }

  public getDebugEmptyMessage(trade: BacktestTrade): string {
    const cycleId = this._getCycleId(trade);
    if (cycleId && this._debugErrors.has(cycleId)) {
      return this._debugErrors.get(cycleId) ?? "Debug data could not be loaded.";
    }

    return "Debug data could not be loaded for this cycle.";
  }

  public getExpandTooltip(trade: BacktestTrade): string {
    if (!this.hasAuditLog) {
      return "Debug data not available for this run.";
    }

    if (!this._getCycleId(trade)) {
      return "Debug data not available for this trade.";
    }

    return this.isExpanded(trade) ? "Hide debug data" : "View debug data";
  }

  public getPnlClass(pnl: number | null): string {
    if (pnl == null) {
      return "";
    }

    return pnl >= 0 ? "trade-log__pnl--profit" : "trade-log__pnl--loss";
  }

  public formatCurrency(value: number | null | undefined): string {
    return value === null || value === undefined ? "—" : `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  public formatRValue(value: number | null | undefined): string {
    return value === null || value === undefined ? "—" : `${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}R`;
  }

  public getSetupBadgeClass(setupDetected: boolean): string {
    return setupDetected ? "trade-log__setup-badge trade-log__setup-badge--positive" : "trade-log__setup-badge";
  }

  public formatDuration(totalMilliseconds: number): string {
    if (totalMilliseconds < 1000) {
      return `${totalMilliseconds}ms`;
    }

    const totalMinutes = Math.floor(totalMilliseconds / 60000);
    if (totalMinutes < 60) {
      return `${totalMinutes}m`;
    }

    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }

  public getOrderEventDetails(event: OrderEvent): string {
    if (event.eventType === OrderEventType.Filled) {
      const fillPrice = event.fillPrice === null ? "—" : this._formatNumber(event.fillPrice, 2, 2);
      const fee = event.fee === null ? "—" : `$${this._formatNumber(event.fee, 4, 4)}`;
      return `Fill @ ${fillPrice}, Fee: ${fee}`;
    }

    if (event.eventType === OrderEventType.Cancelled && event.cancellationReason) {
      return event.cancellationReason;
    }

    if (event.eventType === OrderEventType.Replaced) {
      return `Order ${event.orderId} replaced`;
    }

    return "—";
  }

  public getTradeOrderEvents(debugData: BacktestDebugResponse, trade: BacktestTrade): OrderEvent[] {
    const entryMs = new Date(trade.entryTime).getTime();
    const exitMs = trade.exitTime ? new Date(trade.exitTime).getTime() : Number.MAX_SAFE_INTEGER;
    const bufferMs = 60_000;

    return debugData.orderEvents.filter((event: OrderEvent) => {
      const eventMs = typeof event.timestampUtc === "number" && event.timestampUtc > 1e12
        ? event.timestampUtc
        : event.timestampUtc * 1000;
      return eventMs >= entryMs - bufferMs && eventMs <= exitMs + bufferMs;
    });
  }

  public formatExitReason(reason: string | null | undefined): string {
    if (!reason) {
      return "—";
    }

    switch (reason) {
      case "TakeProfitTriggered":
        return "Take Profit";
      case "StopLossTriggered":
        return "Stop Loss";
      case "GridRedeployed":
        return "Grid Redeployed";
      case "ManualCancel":
        return "Manual Cancel";
      default:
        return reason;
    }
  }

  public getExitReasonClass(reason: string | null | undefined): string {
    if (!reason) {
      return "";
    }

    return reason === "TakeProfitTriggered"
      ? "trade-log__exit-reason--profit"
      : "trade-log__exit-reason--loss";
  }

  public getAvailableSignalTypes(debugData: BacktestDebugResponse): string[] {
    const signalTypes = new Set<string>();

    for (const candle of debugData.candleEvaluations) {
      for (const signal of candle.signalsEmitted) {
        signalTypes.add(signal);
      }
    }

    return Array.from(signalTypes).sort((left: string, right: string) => left.localeCompare(right));
  }

  public getSignalTypeFilter(cycleId: string): string {
    return this._signalTypeFilters.get(cycleId) ?? "";
  }

  public onSignalTypeChange(cycleId: string, event: MatSelectChange): void {
    const value = (event.value as string) ?? "";

    if (!value) {
      this._signalTypeFilters.delete(cycleId);
      return;
    }

    this._signalTypeFilters.set(cycleId, value);
  }

  public getSetupDetectedFilter(cycleId: string): SetupDetectedFilter {
    return this._setupDetectedFilters.get(cycleId) ?? "all";
  }

  public onSetupDetectedFilterChange(cycleId: string, event: MatSelectChange): void {
    const value = (event.value as SetupDetectedFilter) ?? "all";

    if (value === "all") {
      this._setupDetectedFilters.delete(cycleId);
      return;
    }

    this._setupDetectedFilters.set(cycleId, value);
  }

  public getFilteredCandles(debugData: BacktestDebugResponse): CandleEvaluation[] {
    const signalTypeFilter = this.getSignalTypeFilter(debugData.cycleId);
    const setupDetectedFilter = this.getSetupDetectedFilter(debugData.cycleId);

    return debugData.candleEvaluations.filter((candle: CandleEvaluation) => {
      if (signalTypeFilter && !candle.signalsEmitted.includes(signalTypeFilter)) {
        return false;
      }

      if (setupDetectedFilter === "true" && !candle.setupDetected) {
        return false;
      }

      if (setupDetectedFilter === "false" && candle.setupDetected) {
        return false;
      }

      return true;
    });
  }

  public exportJson(debugData: BacktestDebugResponse): void {
    const blob = new Blob([JSON.stringify(debugData, null, 2)], { type: "application/json" });
    this._downloadBlob(blob, `${debugData.cycleId}-debug.json`);
  }

  public exportCsv(debugData: BacktestDebugResponse): void {
    const lines: string[] = [];
    const cycleSummary = debugData.gridCycleSummary;

    lines.push("Section,CycleId,DeployTimestampUtc,AnchorPrice,LevelsPlaced,LevelPrices,LevelsFilled,TakeProfitPrice,StopLossPrice,ExitReason,CyclePnl,CycleDurationMs,CloseTimestampUtc");
    lines.push([
      "GridCycleSummary",
      cycleSummary?.gridCycleId ?? debugData.cycleId,
      cycleSummary?.deployTimestampUtc ?? "",
      cycleSummary?.anchorPrice ?? "",
      cycleSummary?.levelsPlaced ?? "",
      cycleSummary?.levelPrices.join(";") ?? "",
      cycleSummary?.levelsFilled ?? "",
      cycleSummary?.takeProfitPrice ?? "",
      cycleSummary?.stopLossPrice ?? "",
      cycleSummary?.exitReason ?? "",
      cycleSummary?.cyclePnl ?? "",
      cycleSummary?.cycleDurationMs ?? "",
      cycleSummary?.closeTimestampUtc ?? ""
    ].map((value: unknown) => this._escapeCsvValue(value)).join(","));
    lines.push("");

    lines.push("Section,TimestampUtc,EventType,OrderId,Side,OrderType,Price,Size,FillPrice,Fee,IsMaker,CancellationReason,GridCycleId");
    for (const event of debugData.orderEvents) {
      lines.push([
        "OrderEvent",
        event.timestampUtc,
        event.eventType,
        event.orderId,
        event.side,
        event.orderType,
        event.price,
        event.size,
        event.fillPrice ?? "",
        event.fee ?? "",
        event.isMaker ?? "",
        event.cancellationReason ?? "",
        event.gridCycleId
      ].map((value: unknown) => this._escapeCsvValue(value)).join(","));
    }
    lines.push("");

    lines.push("Section,TimestampUtc,Open,High,Low,Close,Volume,IsWarmup,EmaFast,EmaSlow,EmaTrend,Rsi,Atr,SetupDetected,GridLifecycleState,PositionSize,PositionAvgEntry,SignalsEmitted,GridCycleId");
    for (const candle of debugData.candleEvaluations) {
      lines.push([
        "CandleEvaluation",
        candle.timestampUtc,
        candle.open,
        candle.high,
        candle.low,
        candle.close,
        candle.volume,
        candle.isWarmup,
        candle.emaFast,
        candle.emaSlow,
        candle.emaTrend,
        candle.rsi,
        candle.atr,
        candle.setupDetected,
        candle.gridLifecycleState,
        candle.positionSize,
        candle.positionAvgEntry,
        candle.signalsEmitted.join(";"),
        candle.gridCycleId ?? ""
      ].map((value: unknown) => this._escapeCsvValue(value)).join(","));
    }

    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
    this._downloadBlob(blob, `${debugData.cycleId}-debug.csv`);
  }

  private _loadDebugData(cycleId: string): void {
    if (!this.backtestId || this._debugDataCache.has(cycleId) || this._loadingCycleIds.has(cycleId)) {
      return;
    }

    this._debugErrors.delete(cycleId);
    this._loadingCycleIds.add(cycleId);

    const context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
    this._backtestService.getDebugData(this.backtestId, cycleId, context)
      .pipe(finalize(() => this._loadingCycleIds.delete(cycleId)))
      .subscribe({
        next: (debugData: BacktestDebugResponse | null) => {
          this._debugDataCache.set(cycleId, debugData);
        },
        error: () => {
          this._debugErrors.set(cycleId, "Debug data could not be loaded for this cycle.");
        }
      });
  }

  private _getCycleId(trade: BacktestTrade): string | null {
    const cycleId = trade.gridCycleId?.trim();
    return cycleId ? cycleId : null;
  }

  private _getSortValue(trade: BacktestTrade, column: SortableColumn): number | string {
    switch (column) {
      case "entryTime":
        return new Date(trade.entryTime).getTime();
      case "exitTime":
        return trade.exitTime ? new Date(trade.exitTime).getTime() : 0;
      case "entryPrice":
        return trade.entryPrice;
      case "exitPrice":
        return trade.exitPrice ?? 0;
      case "side":
        return trade.side;
      case "size":
        return trade.size;
      case "pnl":
        return trade.pnl ?? 0;
      case "fees":
        return trade.fees;
      case "initialRDollars":
        return trade.initialRDollars ?? 0;
      case "rMultipleResult":
        return trade.rMultipleResult ?? 0;
      case "mfe":
        return trade.mfe ?? 0;
      case "mae":
        return trade.mae ?? 0;
      case "exitReason":
        return trade.exitReason ?? "";
      default:
        return 0;
    }
  }

  private _sortTrades(trades: BacktestTrade[]): BacktestTrade[] {
    if (!this.sortColumn || !this.sortDirection) {
      return trades;
    }

    const column = this.sortColumn;
    const multiplier = this.sortDirection === "asc" ? 1 : -1;

    return [...trades].sort((left: BacktestTrade, right: BacktestTrade) => {
      const leftValue = this._getSortValue(left, column);
      const rightValue = this._getSortValue(right, column);

      if (typeof leftValue === "string" && typeof rightValue === "string") {
        return leftValue.localeCompare(rightValue) * multiplier;
      }

      return ((leftValue as number) - (rightValue as number)) * multiplier;
    });
  }

  private _formatNumber(value: number, minimumFractionDigits: number, maximumFractionDigits: number): string {
    return value.toLocaleString("en-US", {
      minimumFractionDigits,
      maximumFractionDigits,
      useGrouping: false
    });
  }

  private _downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private _escapeCsvValue(value: unknown): string {
    const stringValue = value == null ? "" : String(value);
    const escapedValue = stringValue.replaceAll('"', '""');
    return /[",\n]/.test(escapedValue) ? `"${escapedValue}"` : escapedValue;
  }
}