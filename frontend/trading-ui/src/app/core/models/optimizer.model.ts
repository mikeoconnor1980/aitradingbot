import { BacktestStrategyConfig, PagedResult } from "./backtest.model";

export interface RunOptimizationRequest {
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  sampleSize: number;
  directions?: string[] | null;
  timeframes?: string[] | null;
  stopLossMin?: number | null;
  stopLossMax?: number | null;
  takeProfitMin?: number | null;
  takeProfitMax?: number | null;
  leverageMin?: number | null;
  leverageMax?: number | null;
  positionSizePercent?: number | null;
  rsiOperators?: string[] | null;
  rsiPeriods?: number[] | null;
  rsiThresholds?: number[] | null;
  macdOperators?: string[] | null;
  macdFastPeriods?: number[] | null;
  macdSlowPeriods?: number[] | null;
  priceVsEmaOperators?: string[] | null;
  emaPeriods?: number[] | null;
  emaProximityPercents?: number[] | null;
  exitOnOppositeSignal?: boolean | null;
  maxOpenTradesOptions?: number[] | null;
  cooldownCandlesOptions?: number[] | null;
  includeTrendFilter?: boolean | null;
  minWinRate?: number | null;
  minTotalTrades?: number | null;
  maxDrawdownPercent?: number | null;
  walkForwardEnabled?: boolean | null;
  walkForwardSplitPercent?: number | null;
  evolutionaryEnabled?: boolean | null;
  evolutionaryGenerations?: number | null;
  evolutionaryEliteCount?: number | null;
  evolutionaryMutationRate?: number | null;
  evolutionaryCrossoverRate?: number | null;
}

export interface OptimizationProgress {
  id: string;
  status: string;
  completed: number;
  total: number;
  errorMessage?: string | null;
  phase?: string | null;
  estimatedRemainingMs?: number | null;
}

export interface OptimizationResult {
  rank: number;
  fitnessScore: number;
  signalDescription: string;
  strategyConfigJson: string;
  totalPnl: number;
  winRate: number;
  maxDrawdown: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  totalFeesPaid: number;
  averageTradePnl: number;
  averageHoldTimeMinutes: number;
  sharpeRatio?: number | null;
  sortinoRatio?: number | null;
  profitFactor?: number | null;
  calmarRatio?: number | null;
  oosTotalPnl?: number | null;
  oosWinRate?: number | null;
  oosMaxDrawdown?: number | null;
  oosTotalTrades?: number | null;
  oosFitnessScore?: number | null;
}

export interface OptimizationRun {
  id: string;
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  status: string;
  totalCombinations: number;
  completedCount: number;
  qualifiedCount: number;
  failedCount: number;
  elapsedMs: number;
  errorMessage?: string | null;
  createdAt: string;
  sweepConfigJson?: string | null;
  results: OptimizationResult[];
}

export interface OptimizationRunSummary {
  id: string;
  symbol: string;
  status: string;
  totalCombinations: number;
  completedCount: number;
  qualifiedCount: number;
  failedCount: number;
  elapsedMs: number;
  createdAt: string;
  topFitnessScore?: number | null;
  topTotalPnl?: number | null;
  topWinRate?: number | null;
  topSignalDescription?: string | null;
}

export type OptimizationListResult = PagedResult<OptimizationRunSummary>;

export interface SweepConfigSnapshot {
  Symbol: string;
  StartDateUtc: number;
  EndDateUtc: number;
  InitialCapital: number;
  SampleSize: number;
  Bounds: {
    Directions: number[];
    Timeframes?: string[];
    StopLossMin: number;
    StopLossMax: number;
    TakeProfitMin: number;
    TakeProfitMax: number;
    LeverageMin: number;
    LeverageMax: number;
    PositionSizeOptions: number[];
    RsiOperators: string[];
    RsiPeriods?: number[];
    RsiThresholds?: number[];
    MacdOperators: string[];
    MacdFastPeriods?: number[];
    MacdSlowPeriods?: number[];
    PriceVsEmaOperators: string[];
    EmaPeriods?: number[];
    EmaProximityPercents?: number[];
    ExitOnOppositeSignalOptions: boolean[];
    IncludeTrendFilter: boolean;
    MaxOpenTradesOptions?: number[];
    CooldownCandlesOptions?: number[];
  };
  Thresholds: {
    MinWinRate: number;
    MinTotalTrades: number;
    MaxDrawdownPercent: number;
  };
  WalkForward?: {
    Enabled: boolean;
    SplitPercent?: number;
  };
  Evolutionary?: {
    Enabled: boolean;
    Generations?: number;
    EliteCount?: number;
    MutationRate?: number;
    CrossoverRate?: number;
  };
}

export function parseSweepConfig(json: string | null | undefined): SweepConfigSnapshot | null {
  if (!json) {
    return null;
  }

  try {
    return JSON.parse(json) as SweepConfigSnapshot;
  } catch {
    return null;
  }
}

export function parseOptimizationStrategyConfig(strategyConfigJson: string): BacktestStrategyConfig | null {
  try {
    return JSON.parse(strategyConfigJson) as BacktestStrategyConfig;
  } catch {
    return null;
  }
}