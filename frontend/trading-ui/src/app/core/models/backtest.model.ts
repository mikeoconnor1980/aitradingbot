export interface GridStrategyConfig {
  gridLevels: number;
  gridSpacing: number;
  takeProfitPercent: number;
  breakdownThreshold: number;
  makerFee: number;
  takerFee: number;
  slippage: number;
  positionSize: number;
  leverage: number;
  stopLossPercent: number;
}

export interface BacktestRequest {
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  initialCapital: number;
  strategyConfig: GridStrategyConfig;
}

export interface BacktestTrade {
  entryTime: string;
  exitTime: string | null;
  entryPrice: number;
  exitPrice: number | null;
  side: string;
  size: number;
  pnl: number | null;
  fees: number;
  tradeType: string;
}

export interface EquitySnapshot {
  timestampUtc: number;
  equity: number;
}

export interface BacktestResult {
  id: string;
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  strategyConfig: GridStrategyConfig;
  initialCapital: number;
  candlesReplayed: number;
  elapsedMs: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  totalPnl: number;
  maxDrawdown: number;
  averageTradePnl: number;
  averageHoldTimeMinutes: number;
  hedgesOpened: number;
  totalFeesPaid: number;
  trades: BacktestTrade[];
  createdAt: string;
  equityTimeSeries?: EquitySnapshot[];
}

export interface BacktestSummary {
  id: string;
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  totalTrades: number;
  winRate: number;
  totalPnl: number;
  maxDrawdown: number;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CoverageReport {
  coverage: Record<string, IntervalCoverage>;
}

export interface IntervalCoverage {
  from: string | null;
  to: string | null;
  candleCount: number;
}