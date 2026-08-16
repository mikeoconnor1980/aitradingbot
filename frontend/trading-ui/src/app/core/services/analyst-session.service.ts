import { Injectable, inject, signal } from "@angular/core";
import { Subscription } from "rxjs";
import { AnalystIntent, AnalystRequestContext, TradingAnalystResult } from "../models/analyst.model";
import { AnalystChartContextService } from "./analyst-chart-context.service";
import { AnalystService } from "./analyst.service";

export interface AnalystMessage {
  role: "user" | "analyst" | "error";
  content: string;
  result?: TradingAnalystResult;
}

@Injectable({ providedIn: "root" })
export class AnalystSessionService {
  private readonly _analystService = inject(AnalystService);
  private readonly _chartContext = inject(AnalystChartContextService);
  private _request?: Subscription;

  public readonly messages = signal<AnalystMessage[]>([]);
  public readonly prompt = signal("");
  public readonly isLoading = signal(false);
  public readonly progress = signal("");
  public readonly context = signal<AnalystRequestContext | undefined>(undefined);
  public readonly lastQuestion = signal("");

  public setPrompt(prompt: string): void {
    this.prompt.set(prompt);
  }

  public start(context?: AnalystRequestContext): void {
    this.context.set(context);
  }

  public submit(question: string = this.prompt(), context: AnalystRequestContext | undefined = this.context()): void {
    const trimmedQuestion = question.trim();
    if (!trimmedQuestion || this.isLoading()) {
      return;
    }

    const resolvedContext = this._resolveContext(context);
    if (resolvedContext) {
      this.context.set(resolvedContext);
    }
    this.lastQuestion.set(trimmedQuestion);
    this.messages.update((messages) => [...messages, { role: "user", content: trimmedQuestion }]);
    this.prompt.set("");
    this.isLoading.set(true);
    this.progress.set(this._getProgress(trimmedQuestion, resolvedContext));
    this._request = this._analystService.analyse(trimmedQuestion, resolvedContext).subscribe({
      next: (result: TradingAnalystResult) => {
        this.messages.update((messages) => [...messages, {
          role: result.succeeded ? "analyst" : "error",
          content: result.response || this._getFailureMessage(result.failureCode),
          result
        }]);
        this._completeRequest();
      },
      error: (error: { status?: number }) => {
        this.messages.update((messages) => [...messages, { role: "error", content: this._getHttpErrorMessage(error.status) }]);
        this._completeRequest();
      }
    });
  }

  public cancel(): void {
    if (!this.isLoading()) {
      return;
    }

    this._request?.unsubscribe();
    this._completeRequest();
    this.messages.update((messages) => [...messages, { role: "error", content: "Analysis cancelled." }]);
  }

  public retry(): void {
    this.submit(this.lastQuestion());
  }

  public clear(): void {
    this._request?.unsubscribe();
    this._request = undefined;
    this.messages.set([]);
    this.prompt.set("");
    this.isLoading.set(false);
    this.progress.set("");
    this.context.set(undefined);
    this.lastQuestion.set("");
  }

  public clearContext(): void {
    this.context.set(undefined);
  }

  private _completeRequest(): void {
    this._request = undefined;
    this.isLoading.set(false);
    this.progress.set("");
  }

  private _resolveContext(context: AnalystRequestContext | undefined): AnalystRequestContext | undefined {
    if (context?.intent !== "AnalyseChart") {
      return context;
    }

    const chart = this._chartContext.captureCurrent();
    return chart ? { ...context, chart } : context;
  }

  private _getProgress(question: string, context: AnalystRequestContext | undefined): string {
    const normalized = question.toLowerCase();
    if (normalized.includes("position") || normalized.includes("account")) return "Checking positions...";
    if (normalized.includes("backtest") || normalized.includes("compare")) return "Comparing backtests...";
    if (normalized.includes("strategy") || normalized.includes("rule")) return "Reviewing strategy decisions...";
    return `Analysing ${context?.chart?.symbol ?? "market"}...`;
  }

  private _getFailureMessage(code: string | null | undefined): string {
    return code === "provider_unavailable"
      ? "The AI provider is currently unavailable. Try again shortly."
      : "The analysis could not be completed. Try again.";
  }

  private _getHttpErrorMessage(status: number | undefined): string {
    if (status === 503) return "Market or account data is currently unavailable. Try again shortly.";
    if (status === 0) return "The request was cancelled or the API is unavailable.";
    return "The Analyst request could not be completed. Try again.";
  }
}

/* Duplicate session implementations retained below for recovery while the panel work is consolidated.
import { Injectable, inject, signal } from "@angular/core";
import { Subscription } from "rxjs";
import { AnalystIntent, AnalystRequestContext, TradingAnalystResult } from "../models/analyst.model";
import { AnalystService } from "./analyst.service";

export interface AnalystMessage {
  role: "user" | "analyst" | "error";
  content: string;
  result?: TradingAnalystResult;
}

@Injectable({ providedIn: "root" })
export class AnalystSessionService {
  private readonly _analystService = inject(AnalystService);
  private _request?: Subscription;

  public readonly messages = signal<AnalystMessage[]>([]);
  public readonly prompt = signal("");
  public readonly isLoading = signal(false);
  public readonly progress = signal("");
  public readonly context = signal<AnalystRequestContext | undefined>(undefined);
  public readonly previousRoute = signal<string | undefined>(undefined);
  public readonly lastQuestion = signal("");

  public setPrompt(prompt: string): void {
    this.prompt.set(prompt);
  }

  public start(context?: AnalystRequestContext, question?: string, previousRoute?: string): void {
    this.context.set(context);
    if (previousRoute) {
      this.previousRoute.set(previousRoute);
    }

    if (question) {
      this.submit(question, context);
    }
  }

  public beginContextualInvestigation(context: AnalystRequestContext, question: string, previousRoute: string): void {
    this.clear();
    this.start(context, question, previousRoute);
  }

  public submit(question: string = this.prompt(), context: AnalystRequestContext | undefined = this.context()): void {
    const trimmedQuestion = question.trim();
    if (!trimmedQuestion || this.isLoading()) {
      return;
    }

    if (context) {
      this.context.set(context);
    }
    this.lastQuestion.set(trimmedQuestion);
    this.messages.update((messages) => [...messages, { role: "user", content: trimmedQuestion }]);
    this.prompt.set("");
    this.isLoading.set(true);
    this.progress.set(this._getProgress(trimmedQuestion));
    this._request = this._analystService.analyse(trimmedQuestion, context).subscribe({
      next: (result: TradingAnalystResult) => {
        this.messages.update((messages) => [...messages, {
          role: result.succeeded ? "analyst" : "error",
          content: result.response || this._getFailureMessage(result.failureCode),
          result
        }]);
        this._completeRequest();
      },
      error: (error: { status?: number }) => {
        this.messages.update((messages) => [...messages, { role: "error", content: this._getHttpErrorMessage(error.status) }]);
        this._completeRequest();
      }
    });
  }

  public cancel(): void {
    if (!this.isLoading()) {
      return;
    }

    this._request?.unsubscribe();
    this._completeRequest();
    this.messages.update((messages) => [...messages, { role: "error", content: "Analysis cancelled." }]);
  }

  public retry(): void {
    this.submit(this.lastQuestion());
  }

  public clear(): void {
    this._request?.unsubscribe();
    this._request = undefined;
    this.messages.set([]);
    this.prompt.set("");
    this.isLoading.set(false);
    this.progress.set("");
    this.context.set(undefined);
    this.lastQuestion.set("");
  }

  public clearContext(): void {
    this.context.set(undefined);
  }

  public routeContext(params: { get(name: string): string | null }): AnalystRequestContext | undefined {
    const intent = params.get("intent") as AnalystIntent | null;
    const strategyId = params.get("strategyId");
    const backtestRunId = params.get("backtestRunId");
    const validIntents: AnalystIntent[] = ["ExplainStrategyEntry", "SummariseStrategyBlockingRules", "AnalyseBacktestRun", "CompareBacktestRuns"];
    if (!intent || !validIntents.includes(intent)) {
      return undefined;
    }
    if (strategyId && this._isGuid(strategyId) && !backtestRunId) {
      return { intent, strategyId };
    }
    if (backtestRunId && this._isGuid(backtestRunId) && !strategyId) {
      return { intent, backtestRunId };
    }
    return undefined;
  }

  public questionFor(context: AnalystRequestContext): string {
    return context.intent === "SummariseStrategyBlockingRules"
      ? "Which rules block this strategy most often?"
      : context.intent === "ExplainStrategyEntry"
        ? "Why did this strategy not enter?"
        : context.intent === "CompareBacktestRuns"
          ? "Compare these backtest results."
          : "Analyse this backtest result.";
  }

  private _completeRequest(): void {
    this._request = undefined;
    this.isLoading.set(false);
    this.progress.set("");
  }

  private _getProgress(question: string): string {
    const normalized = question.toLowerCase();
    if (normalized.includes("position") || normalized.includes("account")) return "Checking positions...";
    if (normalized.includes("backtest") || normalized.includes("compare")) return "Comparing backtests...";
    if (normalized.includes("strategy") || normalized.includes("rule")) return "Reviewing strategy decisions...";
    return "Analysing BTC...";
  }

  private _getFailureMessage(code: string | null | undefined): string {
    return code === "provider_unavailable"
      ? "The AI provider is currently unavailable. Try again shortly."
      : "The analysis could not be completed. Try again.";
  }

  private _getHttpErrorMessage(status: number | undefined): string {
    if (status === 503) return "Market or account data is currently unavailable. Try again shortly.";
    if (status === 0) return "The request was cancelled or the API is unavailable.";
    return "The Analyst request could not be completed. Try again.";
  }

  private _isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }
}
*/
