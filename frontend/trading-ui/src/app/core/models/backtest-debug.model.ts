import { ChartIndicatorValues } from "./chart-indicator.model";

export enum OrderEventType {
  Placed = "Placed",
  Filled = "Filled",
  Cancelled = "Cancelled",
  Replaced = "Replaced"
}

export enum CancellationReason {
  GridRedeployed = "GridRedeployed",
  TakeProfitTriggered = "TakeProfitTriggered",
  StopLossTriggered = "StopLossTriggered",
  ManualCancel = "ManualCancel"
}

export interface CandleEvaluation {
  timestampUtc: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  isWarmup: boolean;
  emaFast: number;
  emaSlow: number;
  emaTrend: number;
  rsi: number;
  atr: number;
  setupDetected: boolean;
  gridLifecycleState: string;
  positionSize: number;
  positionAvgEntry: number;
  signalsEmitted: string[];
  gridCycleId: string | null;
  indicators?: ChartIndicatorValues | null;
}

export interface OrderEvent {
  timestampUtc: number;
  eventType: OrderEventType;
  orderId: string;
  side: string;
  orderType: string;
  price: number;
  size: number;
  fillPrice: number | null;
  fee: number | null;
  isMaker: boolean | null;
  cancellationReason: CancellationReason | null;
  gridCycleId: string;
}

export interface GridCycleSummary {
  gridCycleId: string;
  deployTimestampUtc: number;
  anchorPrice: number;
  levelsPlaced: number;
  levelPrices: number[];
  levelsFilled: number;
  takeProfitPrice: number;
  stopLossPrice: number | null;
  exitReason: string;
  cyclePnl: number;
  cycleDurationMs: number;
  closeTimestampUtc: number;
}

export interface BacktestDebugResponse {
  cycleId: string;
  candleEvaluations: CandleEvaluation[];
  orderEvents: OrderEvent[];
  gridCycleSummary: GridCycleSummary | null;
}