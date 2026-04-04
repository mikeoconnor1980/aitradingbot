import { HttpContext, HttpErrorResponse } from "@angular/common/http";
import { CommonModule } from "@angular/common";
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { finalize } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../../../core/interceptors/http-context-tokens";
import { StrategyIntentDto } from "../../models/strategy-intent.model";
import { StrategyApiService } from "../../services/strategy-api.service";

@Component({
  selector: "app-nl-input-card",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: "./nl-input-card.component.html",
  styleUrl: "./nl-input-card.component.scss"
})
export class NlInputCardComponent implements OnChanges {
  private readonly _strategyApi = inject(StrategyApiService);

  @Input() public initialText: string | null = null;
  @Output() public readonly interpreted = new EventEmitter<StrategyIntentDto>();

  public text = "";
  public isLoading = false;
  public errorMessage: string | null = null;
  public readonly maxLength = 2000;

  public ngOnChanges(changes: SimpleChanges): void {
    const initialTextChange = changes["initialText"];
    if (initialTextChange === undefined) {
      return;
    }

    this.text = String(initialTextChange.currentValue ?? "");
  }

  public get charCount(): number {
    return this.text.length;
  }

  public get canGenerate(): boolean {
    return this.text.trim().length > 0 && !this.isLoading;
  }

  public generate(): void {
    if (!this.canGenerate) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

    this._strategyApi.interpretStrategy(this.text.trim(), context)
      .pipe(finalize((): void => {
        this.isLoading = false;
      }))
      .subscribe({
        next: (result: StrategyIntentDto): void => {
          if (this._isFailedInterpretationResult(result)) {
            this.errorMessage = result.clarificationNeeded;
            return;
          }

          this.interpreted.emit(result);
        },
        error: (error: HttpErrorResponse): void => {
          this.errorMessage = this._getErrorMessage(error);
        }
      });
  }

  public clear(): void {
    this.text = "";
    this.errorMessage = null;
  }

  private _getErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 429) {
      return "Too many requests. Please wait a moment before trying again.";
    }

    if (error.status === 400) {
      return "Enter a strategy description before generating.";
    }

    return "Strategy interpretation is temporarily unavailable. Please try again or continue with the form builder.";
  }

  private _isFailedInterpretationResult(result: StrategyIntentDto): boolean {
    const strategyName = result.config.strategyName?.trim() ?? "";

    return result.confidence === 0
      && strategyName.length === 0
      && (result.clarificationNeeded?.trim().length ?? 0) > 0;
  }
}