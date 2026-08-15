import { CommonModule } from "@angular/common";
import { Component, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { ActivatedRoute, Router } from "@angular/router";
import { Subscription } from "rxjs";
import { AnalystIntent, AnalystRequestContext, AnalystToolInvocation, TradingAnalystResult } from "../../core/models/analyst.model";
import { AnalystChartContextService } from "../../core/services/analyst-chart-context.service";
import { AnalystService } from "../../core/services/analyst.service";
import { AnalystEvidenceCardComponent } from "./analyst-evidence-card.component";

interface AnalystMessage {
  role: "user" | "analyst" | "error";
  content: string;
  result?: TradingAnalystResult;
}

@Component({
  selector: "app-analyst-page",
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule, AnalystEvidenceCardComponent],
  templateUrl: "./analyst-page.component.html",
  styleUrl: "./analyst-page.component.scss"
})
export class AnalystPageComponent implements OnInit {
  private readonly _analystService = inject(AnalystService);
  private readonly _router = inject(Router);
  private readonly _route = inject(ActivatedRoute);
  private readonly _chartContext = inject(AnalystChartContextService);
  private _request?: Subscription;

  public readonly suggestions = [
    "What is happening with BTC?",
    "What should I pay attention to in my account?",
    "Why haven't my strategies traded?"
  ];
  public messages: AnalystMessage[] = [];
  public prompt = "";
  public isLoading = false;
  public progress = "";

  public ngOnInit(): void {
    const context = this.getRouteContext();
    if (context) {
      this.submit(this.getContextQuestion(context), context);
      return;
    }

    const chart = this._chartContext.captureCurrent();
    if (chart) {
      this.submit("Explain the visible range.", { intent: "AnalyseChart", chart });
    }
  }

  public submit(question: string = this.prompt, context?: AnalystRequestContext): void {
    const trimmedQuestion = question.trim();
    if (!trimmedQuestion || this.isLoading) {
      return;
    }

    this.messages.push({ role: "user", content: trimmedQuestion });
    this.prompt = "";
    this.isLoading = true;
    this.progress = this.getProgress(trimmedQuestion);
    this._request = this._analystService.analyse(trimmedQuestion, context).subscribe({
      next: (result: TradingAnalystResult) => {
        this.messages.push({
          role: result.succeeded ? "analyst" : "error",
          content: result.response || this.getFailureMessage(result.failureCode),
          result
        });
        this.isLoading = false;
        this.progress = "";
      },
      error: (error: { status?: number }) => {
        this.messages.push({ role: "error", content: this.getHttpErrorMessage(error.status) });
        this.isLoading = false;
        this.progress = "";
      }
    });
  }

  public cancel(): void {
    this._request?.unsubscribe();
    this._request = undefined;
    this.isLoading = false;
    this.progress = "";
    this.messages.push({ role: "error", content: "Analysis cancelled." });
  }

  public evidence(invocations: AnalystToolInvocation[]): AnalystToolInvocation[] {
    return invocations.filter((invocation) => invocation.result || invocation.errorCode);
  }

  public openReference(invocation: AnalystToolInvocation): void {
    const result = invocation.result;
    if (!result) {
      return;
    }

    const symbol = typeof result["symbol"] === "string" ? result["symbol"] : undefined;
    const strategyId = typeof result["strategyId"] === "string" ? result["strategyId"] : undefined;
    const backtestId = typeof result["backtestId"] === "string" ? result["backtestId"] : undefined;
    if (symbol && invocation.toolName.includes("market")) {
      void this._router.navigate(["/market-data"], { queryParams: { symbol } });
    } else if (strategyId) {
      void this._router.navigate(["/strategies", strategyId, "edit"]);
    } else if (backtestId) {
      void this._router.navigate(["/backtesting"], { queryParams: { viewResult: backtestId } });
    }
  }

  public hasReference(invocation: AnalystToolInvocation): boolean {
    const result = invocation.result;
    return !!result && (typeof result["symbol"] === "string" || typeof result["strategyId"] === "string" || typeof result["backtestId"] === "string");
  }

  private getProgress(question: string): string {
    const normalized = question.toLowerCase();
    if (normalized.includes("position") || normalized.includes("account")) return "Checking positions...";
    if (normalized.includes("backtest") || normalized.includes("compare")) return "Comparing backtests...";
    if (normalized.includes("strategy") || normalized.includes("rule")) return "Reviewing strategy decisions...";
    return "Analysing BTC...";
  }

  private getFailureMessage(code: string | null | undefined): string {
    return code === "provider_unavailable"
      ? "The AI provider is currently unavailable."
      : "The analysis could not be completed.";
  }

  private getHttpErrorMessage(status: number | undefined): string {
    if (status === 503) return "Market or account data is currently unavailable.";
    if (status === 0) return "The request was cancelled or the API is unavailable.";
    return "The Analyst request could not be completed.";
  }

  private getRouteContext(): AnalystRequestContext | undefined {
    const params = this._route.snapshot.queryParamMap;
    const intent = params.get("intent") as AnalystIntent | null;
    const strategyId = params.get("strategyId");
    const backtestRunId = params.get("backtestRunId");
    const validIntents: AnalystIntent[] = ["ExplainStrategyEntry", "SummariseStrategyBlockingRules", "AnalyseBacktestRun", "CompareBacktestRuns"];
    if (!intent || !validIntents.includes(intent)) return undefined;
    if (strategyId && this.isGuid(strategyId) && !backtestRunId) return { intent, strategyId };
    if (backtestRunId && this.isGuid(backtestRunId) && !strategyId) return { intent, backtestRunId };
    return undefined;
  }

  private getContextQuestion(context: AnalystRequestContext): string {
    return context.intent === "SummariseStrategyBlockingRules"
      ? "Which rules block this strategy most often?"
      : context.intent === "ExplainStrategyEntry"
        ? "Why did this strategy not enter?"
        : context.intent === "CompareBacktestRuns"
          ? "Compare these backtest results."
          : "Analyse this backtest result.";
  }

  private isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }
}