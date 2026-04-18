export type StrategyMode = "grid" | "signal" | "dca";
export type Direction = "long" | "short" | "both";
export type AssetType = "perp" | "spot";
export type ExitRuleType = "fixed_percent" | "swing_low" | "atr_trailing" | "r_multiple";
export type PositionSizeType = "percent_wallet" | "fixed_notional" | "risk_based";
export type CooldownUnit = "candles" | "minutes";
export type EntryMode = "auto_from_signal_candle" | "manual";
export type DcaInterval = "hourly" | "four_hourly" | "daily" | "weekly" | "biweekly" | "monthly";
export type EntryLogic = "all" | "any";
export type EntryConditionType = "rsi" | "price_vs_ema" | "macd" | "support_resistance";
export type SupportResistanceOperator = "near_support" | "near_resistance" | "above_support" | "below_resistance" | "bounce_support" | "bounce_resistance";
export type RsiOperator = "lt" | "lte" | "gt" | "gte" | "cross_above" | "cross_below";
export type MacdOperator = "cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling";
export type TrendFilterType = "ema_cross" | "sma_cross" | "price_above_ema";
export type TrendOperator = "gt" | "lt" | "gte" | "lte" | "cross_above" | "cross_below" | "above" | "below";
export type PriceVsEmaOperator = "near" | "above" | "below" | "cross_above" | "cross_below" | "touch";
export type PriceVsEmaDistanceType = "percent" | "atr_multiple" | "absolute";

export interface GridConfig {
  levels: number;
  spacing: number;
  entryMode: EntryMode;
  anchorPrice?: number | null;
  breakdownThreshold: number;
}

export interface DcaGateConfig {
  maxPriceUsd?: number | null;
  minFearGreedIndex?: number | null;
  maxFearGreedIndex?: number | null;
}

export interface DcaScalingBand {
  priceLowerUsd?: number | null;
  priceUpperUsd?: number | null;
  scalingPercent: number;
}

export interface DcaAllocation {
  market: string;
  weightPercent: number;
}

export interface DcaConfig {
  interval: DcaInterval;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  timeOfDayUtc: string;
  baseAmountUsd: number;
  allocations: DcaAllocation[];
  gateConditions?: DcaGateConfig | null;
  scalingBands?: DcaScalingBand[] | null;
  profitTaking?: unknown | null;
  budgetCapUsd?: number | null;
}

export interface RsiParams {
  period: number;
  operator: RsiOperator;
  value: number;
}

export interface TrendFilterConfig {
  enabled: boolean;
  type: TrendFilterType;
  period?: number | null;
  fastPeriod: number;
  slowPeriod: number;
  operator: TrendOperator;
  appliesTo: Direction;
}

export interface PriceVsEmaParams {
  period: number;
  operator: PriceVsEmaOperator;
  distanceType: PriceVsEmaDistanceType;
  distanceValue: number | null;
}

export interface MacdParams {
  fastPeriod: number;
  slowPeriod: number;
  signalPeriod: number;
  operator: MacdOperator;
}

export interface SupportResistanceParams {
  lookback: number;
  strength: number;
  operator: SupportResistanceOperator;
  tolerance: number;
}

export interface EntryConditionConfig {
  id: string;
  enabled: boolean;
  type: EntryConditionType;
  label: string;
  params: RsiParams | PriceVsEmaParams | MacdParams | SupportResistanceParams;
}

export interface ExitRuleConfig {
  enabled: boolean;
  type: ExitRuleType;
  value?: number | null;
  lookback?: number | null;
  atrMultiplier?: number | null;
  trailingStopWarmup?: number | null;
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
  riskPerTradePercent?: number;
  autoLeverage?: boolean;
}

export interface StrategyMetadata {
  tags: string[];
  notes: string;
}

export interface SourceMetadata {
  entryPoint: string;
  summary: string;
  sourceText?: string | null;
}

export interface StrategyConfig {
  schemaVersion: number;
  strategyMode: StrategyMode;
  strategyName: string;
  exchange: string;
  assetType?: AssetType | null;
  market: string;
  timeframe: string;
  direction: Direction;
  enabled: boolean;
  templateId?: string | null;
  grid?: GridConfig | null;
  dca?: DcaConfig | null;
  trendFilter?: TrendFilterConfig | null;
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
  { id: "dca", label: "DCA Spot", available: true },
  { id: "custom_signal", label: "Custom Signal", available: true },
  { id: "ema_pullback", label: "EMA Pullback", available: true },
  { id: "macd_cross", label: "MACD Cross", available: true },
  { id: "rsi_reversal", label: "RSI Reversal", available: false },
  { id: "blank", label: "Blank", available: true },
];