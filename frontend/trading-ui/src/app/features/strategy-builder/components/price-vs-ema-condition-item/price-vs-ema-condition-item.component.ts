import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import {
  PRICE_VS_EMA_OPERATORS,
  PriceVsEmaOperatorOption,
} from "../../enums/price-vs-ema-operator.enum";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-price-vs-ema-condition-item",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./price-vs-ema-condition-item.component.html",
  styleUrl: "./price-vs-ema-condition-item.component.scss"
})
export class PriceVsEmaConditionItemComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);

  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public readonly duplicate = new EventEmitter<void>();
  @Output() public readonly remove = new EventEmitter<void>();

  public readonly operators: PriceVsEmaOperatorOption[] = PRICE_VS_EMA_OPERATORS;

  public get showDistanceFields(): boolean {
    return this.group.get("operator")?.value === "near";
  }

  public ngOnInit(): void {
    this._syncDistanceFieldVisibility();
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public onDuplicate(): void {
    this.duplicate.emit();
  }

  public onRemove(): void {
    this.remove.emit();
  }

  private _syncDistanceFieldVisibility(): void {
    const operatorControl = this.group.get("operator");
    if (operatorControl === null) {
      return;
    }

    this._setDistanceFieldState(String(operatorControl.value ?? "near"));

    operatorControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((value: string) => {
        this._setDistanceFieldState(value);
      });
  }

  private _setDistanceFieldState(operator: string): void {
    const distanceType = this.group.get("distanceType");
    const distanceValue = this.group.get("distanceValue");
    if (distanceType === null || distanceValue === null) {
      return;
    }

    if (operator === "near") {
      distanceType.enable({ emitEvent: false });
      distanceValue.enable({ emitEvent: false });
      return;
    }

    distanceType.disable({ emitEvent: false });
    distanceValue.disable({ emitEvent: false });
  }
}