import { Component, inject } from "@angular/core";
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { StrategyConfig } from "../../core/services/agent.service";

export interface StartTradingDialogData {
  agentId: string;
}

export interface StartTradingDialogResult {
  strategyConfig: StrategyConfig;
}

@Component({
  selector: "app-start-trading-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Start Trading</h2>
    <mat-dialog-content>
      <p>Configure strategy for agent <strong>{{ data.agentId }}</strong></p>

      <form [formGroup]="form" class="start-dialog__form">
        <mat-form-field appearance="outline">
          <mat-label>Strategy Name</mat-label>
          <input matInput formControlName="strategyName" placeholder="Grid-BTC-15m" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Market</mat-label>
          <mat-select formControlName="market">
            <mat-option value="BTC-PERP">BTC-PERP</mat-option>
            <mat-option value="ETH-PERP">ETH-PERP</mat-option>
            <mat-option value="SOL-PERP">SOL-PERP</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Timeframe</mat-label>
          <mat-select formControlName="timeframe">
            <mat-option value="15m">15m</mat-option>
            <mat-option value="1h">1h</mat-option>
            <mat-option value="4h">4h</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Direction</mat-label>
          <mat-select formControlName="direction">
            <mat-option value="Long">Long</mat-option>
            <mat-option value="Short">Short</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Grid Levels</mat-label>
          <input matInput type="number" formControlName="gridLevels" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Grid Spacing %</mat-label>
          <input matInput type="number" formControlName="gridSpacing" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Notional Per Level ($)</mat-label>
          <input matInput type="number" formControlName="notionalPerLevel" />
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="form.invalid"
        (click)="onStart()">
        Start Trading
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .start-dialog__form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-top: 12px;
      min-width: 400px;
    }
  `]
})
export class StartTradingDialogComponent {
  public readonly data = inject<StartTradingDialogData>(MAT_DIALOG_DATA);
  private readonly _dialogRef = inject(MatDialogRef<StartTradingDialogComponent>);
  private readonly _fb = inject(FormBuilder);

  public readonly form: FormGroup = this._fb.group({
    strategyName: ["Grid-BTC-15m", Validators.required],
    market: ["BTC-PERP", Validators.required],
    timeframe: ["15m", Validators.required],
    direction: ["Long", Validators.required],
    gridLevels: [5, [Validators.required, Validators.min(1)]],
    gridSpacing: [0.5, [Validators.required, Validators.min(0.1)]],
    notionalPerLevel: [100, [Validators.required, Validators.min(10)]],
  });

  public onStart(): void {
    if (this.form.invalid) return;

    const v = this.form.value;
    const result: StartTradingDialogResult = {
      strategyConfig: {
        strategyName: v.strategyName,
        strategyMode: "Grid",
        exchange: "Hyperliquid",
        market: v.market,
        timeframe: v.timeframe,
        direction: v.direction,
        enabled: true,
        grid: {
          levels: v.gridLevels,
          spacingPercent: v.gridSpacing,
          notionalPerLevel: v.notionalPerLevel,
        },
        risk: {
          maxPositionSize: 1000,
          maxDrawdownPercent: 10,
        },
      }
    };

    this._dialogRef.close(result);
  }
}
