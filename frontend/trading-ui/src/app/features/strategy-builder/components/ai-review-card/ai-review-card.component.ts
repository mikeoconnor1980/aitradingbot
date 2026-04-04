import { DatePipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { marked } from "marked";
import { StrategyReviewDto } from "../../models/strategy-review.model";

@Component({
  selector: "app-ai-review-card",
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatExpansionModule, MatIconModule, MatTooltipModule],
  templateUrl: "./ai-review-card.component.html",
  styleUrl: "./ai-review-card.component.scss"
})
export class AiReviewCardComponent {
  private readonly _summaryLength = 500;

  @Input()
  public review: StrategyReviewDto | null = null;

  @Output()
  public readonly viewFullReview = new EventEmitter<void>();

  public get isTruncated(): boolean {
    return (this.review?.reviewMarkdown.length ?? 0) > this._summaryLength;
  }

  public get summaryHtml(): string {
    if (this.review === null) {
      return "";
    }

    return String(marked.parse(this._buildSummaryMarkdown(this.review.reviewMarkdown), { async: false }));
  }

  public onViewFull(): void {
    this.viewFullReview.emit();
  }

  private _buildSummaryMarkdown(markdown: string): string {
    if (markdown.length <= this._summaryLength) {
      return markdown;
    }

    const truncated = markdown.slice(0, this._summaryLength);
    const lastBreakIndex = Math.max(truncated.lastIndexOf("\n"), truncated.lastIndexOf(" "));
    const safeSummary = lastBreakIndex > 0 ? truncated.slice(0, lastBreakIndex) : truncated;

    return `${safeSummary.trimEnd()}...`;
  }
}