export type BacktestEntryMode = "AutoFromSignalCandle" | "InitialMarketThenGrid" | "WaitForLimitPrice";

export interface BacktestGridConfig {
  levels: number;
  entryMode: BacktestEntryMode;
  anchorPrice?: number | null;
  spacing: number;
  breakdownThreshold: number;
}

export interface BacktestExitRuleConfig {
  enabled: boolean;
  type: string;
  value?: number | null;
  lookback?: number | null;
}

export interface BacktestExitConfig {
  takeProfit: BacktestExitRuleConfig;
  stopLoss: BacktestExitRuleConfig;
  exitOnOppositeSignal: boolean;
}

export interface BacktestRiskConfig {
  positionSizeType: string;
  positionSizeValue: number;
  leverage: number;
  maxOpenTrades: number;
  cooldownValue: number;
  cooldownUnit: string;
  allowSameCandleReentry: boolean;
}

export interface BacktestSourceMetadata {
  entryPoint: string;
  summary?: string;
}

export interface BacktestStrategyConfig {
  schemaVersion: number;
  strategyMode: string;
  strategyName: string;
  exchange: string;
  market: string;
  timeframe: string;
  direction: string;
  enabled: boolean;
  templateId?: string | null;
  grid?: BacktestGridConfig | null;
  trendFilter?: null;
  entryLogic?: null;
  entryConditions?: null;
  exit: BacktestExitConfig;
  risk: BacktestRiskConfig;
  metadata?: { tags: string[]; notes: string } | null;
  source?: BacktestSourceMetadata | null;
}

export interface FeeModel {
  makerFeeRate: number;
  takerFeeRate: number;
  slippageRate: number;
}

export interface ExecutionConfig {
  feeModel: FeeModel;
  leverage?: number;
}

export interface BacktestExecutionConfigRequest {
  makerFee: number;
  takerFee: number;
  slippage: number;
}

export interface BacktestRequest {
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  initialCapital: number;
  strategyConfig: BacktestStrategyConfig;
  executionConfig: BacktestExecutionConfigRequest;
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
  gridCycleId?: string | null;
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
  strategyConfig: BacktestStrategyConfig;
  executionConfig: ExecutionConfig;
  initialCapital: number;
  status: string;
  progress: number;
  errorMessage?: string | null;
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
  hasAuditLog: boolean;
}

export interface BacktestProgress {
  id: string;
  status: string;
  progress: number;
  totalCandles: number;
  errorMessage?: string | null;
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