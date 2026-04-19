import { Component, OnInit, inject } from "@angular/core";
import { ReactiveFormsModule, FormControl, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatSelectModule } from "@angular/material/select";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { StrategyApiService } from "../strategy-builder/services/strategy-api.service";
import { StrategySummaryDto } from "../strategy-builder/models/strategy.model";

export interface StartTradingDialogData {
  agentId: string;
}

export interface StartTradingDialogResult {
  strategyId: string;
}

@Component({
  selector: "app-start-trading-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>Start Trading</h2>
    <mat-dialog-content>
      <p>Select a strategy for agent <strong>{{ data.agentId }}</strong></p>

      @if (isLoadingStrategies) {
        <div class="start-dialog__loading">
          <mat-spinner diameter="32"></mat-spinner>
          <span>Loading strategies...</span>
        </div>
      } @else if (strategies.length === 0) {
        <p class="start-dialog__empty">No saved strategies found. Create one in the Strategy Builder first.</p>
      } @else {
        <mat-form-field appearance="outline" class="start-dialog__select">
          <mat-label>Strategy</mat-label>
          <mat-select [formControl]="strategyControl">
            @for (s of strategies; track s.id) {
              <mat-option [value]="s.id">
                {{ s.name }}
                <span class="start-dialog__meta">{{ s.market }} · {{ s.timeframe }} · {{ s.direction }}</span>
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (selectedSummary) {
          <div class="start-dialog__preview">
            <div><strong>Mode:</strong> {{ selectedSummary.strategyMode }}</div>
            <div><strong>Market:</strong> {{ selectedSummary.market }}</div>
            <div><strong>Timeframe:</strong> {{ selectedSummary.timeframe }}</div>
            <div><strong>Direction:</strong> {{ selectedSummary.direction }}</div>
          </div>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="strategyControl.invalid || isStarting"
        (click)="onStart()">
        @if (isStarting) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          Start Trading
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .start-dialog__select {
      width: 100%;
      margin-top: 12px;
    }
    .start-dialog__meta {
      font-size: 12px;
      opacity: 0.6;
      margin-left: 8px;
    }
    .start-dialog__preview {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 4px 16px;
      margin-top: 12px;
      padding: 12px;
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.04);
      font-size: 14px;
    }
    .start-dialog__loading {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 24px 0;
    }
    .start-dialog__empty {
      opacity: 0.6;
      padding: 16px 0;
    }
    mat-dialog-content {
      min-width: 400px;
    }
  `]
})
export class StartTradingDialogComponent implements OnInit {
  public readonly data = inject<StartTradingDialogData>(MAT_DIALOG_DATA);
  private readonly _dialogRef = inject(MatDialogRef<StartTradingDialogComponent>);
  private readonly _strategyApi = inject(StrategyApiService);

  public readonly strategyControl = new FormControl<string | null>(null, Validators.required);
  public strategies: StrategySummaryDto[] = [];
  public isLoadingStrategies = true;
  public isStarting = false;

  public get selectedSummary(): StrategySummaryDto | null {
    const id = this.strategyControl.value;
    return id ? this.strategies.find(s => s.id === id) ?? null : null;
  }

  public ngOnInit(): void {
    this._strategyApi.getStrategies().subscribe({
      next: (strategies) => {
        this.strategies = strategies;
        this.isLoadingStrategies = false;
      },
      error: () => {
        this.isLoadingStrategies = false;
      }
    });
  }

  public onStart(): void {
    const strategyId = this.strategyControl.value;
    if (!strategyId) return;

    const result: StartTradingDialogResult = {
      strategyId,
    };

    this._dialogRef.close(result);
  }
}
