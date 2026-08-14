export interface AnalystQuestionRequest {
  question: string;
  context?: AnalystRequestContext;
}

export type AnalystIntent = "ExplainStrategyEntry" | "SummariseStrategyBlockingRules" | "AnalyseBacktestRun" | "CompareBacktestRuns";

export interface AnalystRequestContext {
  intent: AnalystIntent;
  strategyId?: string;
  strategyVersion?: number;
  backtestRunId?: string;
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