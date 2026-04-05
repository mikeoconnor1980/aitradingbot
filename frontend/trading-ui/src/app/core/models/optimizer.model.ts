import { BacktestStrategyConfig, PagedResult } from "./backtest.model";

export interface RunOptimizationRequest {
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  sampleSize: number;
  stopLossMin?: number | null;
  stopLossMax?: number | null;
  takeProfitMin?: number | null;
  takeProfitMax?: number | null;
  leverageMin?: number | null;
  leverageMax?: number | null;
  minWinRate?: number | null;
  minTotalTrades?: number | null;
  maxDrawdownPercent?: number | null;
}

export interface OptimizationProgress {
  id: string;
  status: string;
  completed: number;
  total: number;
  errorMessage?: string | null;
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
  elapsedMs: number;
  errorMessage?: string | null;
  createdAt: string;
  results: OptimizationResult[];
}

export interface OptimizationRunSummary {
  id: string;
  symbol: string;
  status: string;
  totalCombinations: number;
  completedCount: number;
  qualifiedCount: number;
  elapsedMs: number;
  createdAt: string;
  topFitnessScore?: number | null;
  topTotalPnl?: number | null;
  topWinRate?: number | null;
  topSignalDescription?: string | null;
}

export type OptimizationListResult = PagedResult<OptimizationRunSummary>;

export function parseOptimizationStrategyConfig(strategyConfigJson: string): BacktestStrategyConfig | null {
  try {
    return JSON.parse(strategyConfigJson) as BacktestStrategyConfig;
  } catch {
    return null;
  }
}