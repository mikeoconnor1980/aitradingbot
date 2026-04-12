import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatRadioModule } from "@angular/material/radio";

export interface KillSwitchDialogData {
  agentId: string;
}

export interface KillSwitchDialogResult {
  reason?: string;
  effectiveAtUtc?: string;
}

@Component({
  selector: "app-kill-switch-dialog",
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
  ],
  template: `
    <h2 mat-dialog-title>Kill Switch</h2>
    <mat-dialog-content>
      <p>This will force the agent to stop trading and prevent it from reconnecting until reinstated.</p>

      <mat-radio-group [(ngModel)]="timing" class="kill-switch__timing">
        <mat-radio-button value="now">Kill immediately</mat-radio-button>
        <mat-radio-button value="scheduled">Schedule kill at date/time</mat-radio-button>
      </mat-radio-group>

      @if (timing === "scheduled") {
        <mat-form-field appearance="outline" class="kill-switch__field">
          <mat-label>Kill at (UTC)</mat-label>
          <input matInput type="datetime-local" [(ngModel)]="scheduledDateTime">
        </mat-form-field>
      }

      <mat-form-field appearance="outline" class="kill-switch__field">
        <mat-label>Reason (optional)</mat-label>
        <input matInput [(ngModel)]="reason" placeholder="e.g. Subscription expired">
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="warn" (click)="onConfirm()" [disabled]="timing === 'scheduled' && !scheduledDateTime">
        {{ timing === "scheduled" ? "Schedule Kill" : "Kill Now" }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .kill-switch__timing {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 16px 0;
    }

    .kill-switch__field {
      width: 100%;
      margin-top: 12px;
    }
  `],
})
export class KillSwitchDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<KillSwitchDialogComponent>);

  public timing: "now" | "scheduled" = "now";
  public scheduledDateTime = "";
  public reason = "";

  public onConfirm(): void {
    const result: KillSwitchDialogResult = {};

    if (this.reason.trim()) {
      result.reason = this.reason.trim();
    }

    if (this.timing === "scheduled" && this.scheduledDateTime) {
      result.effectiveAtUtc = new Date(this.scheduledDateTime).toISOString();
    }

    this._dialogRef.close(result);
  }
}
