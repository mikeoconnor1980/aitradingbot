import { Component, Input, OnInit, inject } from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { ReferenceDataService } from "../../services/reference-data.service";

@Component({
  selector: "app-strategy-details-card",
  standalone: true,
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: "./strategy-details-card.component.html",
  styleUrl: "./strategy-details-card.component.scss"
})
export class StrategyDetailsCardComponent implements OnInit {
  private readonly _referenceDataService = inject(ReferenceDataService);

  @Input({ required: true }) public group!: FormGroup;

  public markets: string[] = [];
  public timeframes: string[] = [];

  public ngOnInit(): void {
    this._referenceDataService.getReferenceData().subscribe((referenceData) => {
      this.markets = referenceData.markets;
      this.timeframes = referenceData.timeframes;
    });
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }
}