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
        if (enabled) {
          valueControl.enable();
          return;
        }

        valueControl.disable();
      });

    if (!enabledControl.value) {
      valueControl.disable();
    }
  }

  public hasError(path: string, errorCode: string): boolean {
    const control = this.group.get(path);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }
}