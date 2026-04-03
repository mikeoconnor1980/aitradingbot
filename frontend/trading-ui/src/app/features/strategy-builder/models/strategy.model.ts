export type StrategyMode = "grid" | "signal";
export type Direction = "long" | "short" | "both";
export type ExitRuleType = "fixed_percent" | "swing_low";
export type PositionSizeType = "percent_wallet" | "fixed_notional";
export type CooldownUnit = "candles" | "minutes";
export type EntryMode = "auto_from_signal_candle" | "manual";
export type EntryLogic = "all" | "any";
export type EntryConditionType = "rsi" | "price_vs_ema" | "macd";
export type RsiOperator = "lt" | "lte" | "gt" | "gte" | "cross_above" | "cross_below";

export interface GridConfig {
  levels: number;
  spacing: number;
  entryMode: EntryMode;
  anchorPrice?: number | null;
  breakdownThreshold: number;
}

export interface RsiParams {
  period: number;
  operator: RsiOperator;
  value: number;
}

export interface EntryConditionConfig {
  id: string;
  enabled: boolean;
  type: EntryConditionType;
  label: string;
  params: RsiParams;
}

export interface ExitRuleConfig {
  enabled: boolean;
  type: ExitRuleType;
  value?: number | null;
  lookback?: number | null;
}

export interface ExitConfig {
  takeProfit: ExitRuleConfig;
  stopLoss: ExitRuleConfig;
  exitOnOppositeSignal: boolean;
}

export interface RiskConfig {
  positionSizeType: PositionSizeType;
  positionSizeValue: number;
  leverage: number;
  maxOpenTrades: number;
  cooldownValue: number;
  cooldownUnit: CooldownUnit;
  allowSameCandleReentry: boolean;
}

export interface StrategyMetadata {
  tags: string[];
  notes: string;
}

export interface SourceMetadata {
  entryPoint: string;
  summary: string;
}

export interface StrategyConfig {
  schemaVersion: number;
  strategyMode: StrategyMode;
  strategyName: string;
  exchange: string;
  market: string;
  timeframe: string;
  direction: Direction;
  enabled: boolean;
  templateId?: string | null;
  grid?: GridConfig | null;
  trendFilter?: null;
  entryLogic?: EntryLogic | null;
  entryConditions?: EntryConditionConfig[] | null;
  exit: ExitConfig;
  risk: RiskConfig;
  metadata?: StrategyMetadata | null;
  source?: SourceMetadata | null;
}

export interface StrategyDto {
  id: string;
  name: string;
  strategyType: string;
  config: StrategyConfig;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface StrategySummaryDto {
  id: string;
  name: string;
  market: string;
  timeframe: string;
  direction: string;
  strategyMode: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface StrategyRevisionSummaryDto {
  revisionNumber: number;
  source: string;
  label: string | null;
  changeSummary: string;
  createdAt: string;
}

export interface StrategyRevisionDto {
  revisionNumber: number;
  source: string;
  label: string | null;
  changeSummary: string;
  createdAt: string;
  config: StrategyConfig;
}

export interface FieldChangeDto {
  path: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface StrategyDiffDto {
  fromRevision: number;
  toRevision: number;
  changes: FieldChangeDto[];
}

export interface ReferenceDataResponse {
  markets: string[];
  timeframes: string[];
}

export interface ValidationError {
  severity: "error" | "warning" | "info";
  fieldPath: string;
  code: string;
  message: string;
}

export interface ServerValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationError[];
  infoMessages: ValidationError[];
}

export interface StrategyTemplate {
  id: string;
  label: string;
  available: boolean;
}

export const STRATEGY_TEMPLATES: StrategyTemplate[] = [
  { id: "grid", label: "Grid", available: true },
  { id: "custom_signal", label: "Custom Signal", available: true },
  { id: "ema_pullback", label: "EMA Pullback", available: false },
  { id: "rsi_reversal", label: "RSI Reversal", available: false },
  { id: "blank", label: "Blank", available: true },
];