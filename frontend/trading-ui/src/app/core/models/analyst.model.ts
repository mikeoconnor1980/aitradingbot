export interface AnalystQuestionRequest {
  question: string;
  context?: AnalystRequestContext;
}

export type AnalystIntent = "ExplainStrategyEntry" | "SummariseStrategyBlockingRules" | "AnalyseBacktestRun" | "CompareBacktestRuns" | "AnalyseChart";

export type ChartIndicatorId = "EMA20" | "EMA50" | "EMA200" | "BOLLINGER20_2" | "RSI14" | "MACD12_26_9";
export type ChartOverlayId = "TRADE_MARKERS";

export interface AnalystChartContext {
  symbol: string;
  timeframe: string;
  visibleFromOpenTimeUtc: string;
  visibleToOpenTimeUtc: string;
  selectedCandleOpenTimeUtc?: string;
  activeIndicators: ChartIndicatorId[];
  visibleOverlays: ChartOverlayId[];
  capturedAtUtc: string;
}

export interface AnalystRequestContext {
  intent: AnalystIntent;
  strategyId?: string;
  strategyVersion?: number;
  backtestRunId?: string;
  chart?: AnalystChartContext;
}

export interface AnalystToolInvocation {
  toolCallId: string;
  toolName: string;
  arguments: string;
  succeeded: boolean;
  duration: string;
  errorCode?: string | null;
  wasCached: boolean;
  result?: Record<string, unknown> | null;
}

export interface TradingAnalystResult {
  response: string;
  toolInvocations: AnalystToolInvocation[];
  succeeded: boolean;
  failureCode?: string | null;
}