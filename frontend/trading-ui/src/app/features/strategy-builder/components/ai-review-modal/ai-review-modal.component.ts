import { DatePipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { marked } from "marked";
import { StrategyReviewDto } from "../../models/strategy-review.model";

export interface AiReviewModalData {
  review: StrategyReviewDto;
}

@Component({
  selector: "app-ai-review-modal",
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatIconModule, MatTooltipModule],
  templateUrl: "./ai-review-modal.component.html",
  styleUrl: "./ai-review-modal.component.scss"
})
export class AiReviewModalComponent {
  private readonly _dialogRef = inject(MatDialogRef<AiReviewModalComponent>);

  public readonly data: AiReviewModalData = inject(MAT_DIALOG_DATA);

  public get reviewHtml(): string {
    return String(marked.parse(this.data.review.reviewMarkdown, { async: false }));
  }

  public onClose(): void {
    this._dialogRef.close();
  }
}