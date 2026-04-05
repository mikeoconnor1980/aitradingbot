import { Component, DestroyRef, Input, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-exit-rules-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./exit-rules-card.component.html",
  styleUrl: "./exit-rules-card.component.scss"
})
export class ExitRulesCardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);

  @Input({ required: true }) public group!: FormGroup;

  public ngOnInit(): void {
    this._syncDisabledState("takeProfit");
    this._syncDisabledState("stopLoss");
    this._syncStopLossType();
  }

  public get isSwingLowStopLoss(): boolean {
    return this.group.get("stopLoss.type")?.value === "swing_low";
  }

  public get isAtrTrailingStopLoss(): boolean {
    return this.group.get("stopLoss.type")?.value === "atr_trailing";
  }

  private _syncDisabledState(groupName: string): void {
    const enabledControl = this.group.get(`${groupName}.enabled`);
    const valueControl = this.group.get(`${groupName}.value`);

    if (enabledControl === null || valueControl === null) {
      return;
    }

    enabledControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((enabled: boolean) => {
        if (groupName === "stopLoss") {
          this._applyStopLossType(enabled);
          return;
        }

        if (enabled) {
          valueControl.enable();
          return;
        }

        valueControl.disable();
      });

    if (groupName === "stopLoss") {
      this._applyStopLossType(Boolean(enabledControl.value));
      return;
    }

    if (!enabledControl.value) {
      valueControl.disable();
    }
  }

  private _syncStopLossType(): void {
    const typeControl = this.group.get("stopLoss.type");
    const enabledControl = this.group.get("stopLoss.enabled");

    if (typeControl === null || enabledControl === null) {
      return;
    }

    typeControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyStopLossType(Boolean(enabledControl.value));
      });
  }

  private _applyStopLossType(enabled: boolean): void {
    const valueControl = this.group.get("stopLoss.value");
    const lookbackControl = this.group.get("stopLoss.lookback");
    const atrMultiplierControl = this.group.get("stopLoss.atrMultiplier");
    const warmupControl = this.group.get("stopLoss.trailingStopWarmup");

    if (valueControl === null || lookbackControl === null || atrMultiplierControl === null || warmupControl === null) {
      return;
    }

    if (!enabled) {
      valueControl.disable();
      lookbackControl.disable();
      atrMultiplierControl.disable();
      warmupControl.disable();
      return;
    }

    if (this.isAtrTrailingStopLoss) {
      valueControl.disable();
      lookbackControl.disable();
      atrMultiplierControl.enable();
      warmupControl.enable();
      return;
    }

    if (this.isSwingLowStopLoss) {
      valueControl.disable();
      lookbackControl.enable();
      atrMultiplierControl.disable();
      warmupControl.disable();
      return;
    }

    valueControl.enable();
    lookbackControl.disable();
    atrMultiplierControl.disable();
    warmupControl.disable();
  }

  public hasError(path: string, errorCode: string): boolean {
    const control = this.group.get(path);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }
}