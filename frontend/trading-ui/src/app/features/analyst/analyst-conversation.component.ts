import { CommonModule } from "@angular/common";
import { AfterViewChecked, Component, ElementRef, Input, ViewChild, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { Router } from "@angular/router";
import { AnalystToolInvocation } from "../../core/models/analyst.model";
import { AnalystSessionService } from "../../core/services/analyst-session.service";
import { AnalystEvidenceCardComponent } from "./analyst-evidence-card.component";

@Component({
  selector: "app-analyst-conversation",
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, AnalystEvidenceCardComponent],
  templateUrl: "./analyst-conversation.component.html",
  styleUrl: "./analyst-conversation.component.scss"
})
export class AnalystConversationComponent implements AfterViewChecked {
  private readonly _router = inject(Router);
  private _shouldScroll = true;

  @Input()
  public compact = false;

  @ViewChild("composer")
  public composer?: ElementRef<HTMLTextAreaElement>;

  @ViewChild("transcript")
  public transcript?: ElementRef<HTMLElement>;

  public readonly session = inject(AnalystSessionService);

  public get suggestions(): string[] {
    const context = this.session.context();
    if (context?.intent === "ExplainStrategyEntry") {
      return ["Why did this strategy not enter?", "Which rules block this strategy most often?"];
    }
    if (context?.intent === "AnalyseBacktestRun" || context?.intent === "CompareBacktestRuns") {
      return context.intent === "CompareBacktestRuns" ? ["Compare these runs", "What explains the difference?"] : ["Explain this result", "What should I investigate next?"];
    }
    return ["What needs my attention?", "Explain the current BTC regime", "Why have my strategies not traded?"];
  }

  public ngAfterViewChecked(): void {
    if (this._shouldScroll && this.transcript) {
      this.transcript.nativeElement.scrollTop = this.transcript.nativeElement.scrollHeight;
      this._shouldScroll = false;
    }
  }

  public focusComposer(): void {
    this.composer?.nativeElement.focus();
  }

  public onPromptChange(prompt: string): void {
    this.session.setPrompt(prompt);
    this.resizeComposer();
  }

  public onSubmit(): void {
    this.session.submit();
    this._shouldScroll = true;
  }

  public onSuggestion(suggestion: string): void {
    this.session.submit(suggestion);
    this._shouldScroll = true;
  }

  public onKeydown(event: KeyboardEvent): void {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      this.onSubmit();
    }
  }

  public evidence(invocations: AnalystToolInvocation[]): AnalystToolInvocation[] {
    return invocations.filter((invocation) => invocation.result || invocation.errorCode);
  }

  public shouldExpandEvidence(content: string): boolean {
    return /\b(explain|why|compare|evidence|rule)\b/i.test(content);
  }

  public contextLabel(): string {
    const context = this.session.context();
    if (!context) return "General workspace";
    if (context.strategyId) return "Strategy context";
    if (context.backtestRunId) return "Backtest context";
    return "Current context";
  }

  public openReference(invocation: AnalystToolInvocation): void {
    const result = invocation.result;
    if (!result) return;

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

  private resizeComposer(): void {
    const composer = this.composer?.nativeElement;
    if (!composer) return;
    composer.style.height = "auto";
    composer.style.height = `${Math.min(composer.scrollHeight, 128)}px`;
  }
}