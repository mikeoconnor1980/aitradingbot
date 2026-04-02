import { Component, Input } from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";

@Component({
  selector: "app-grid-config-card",
  standalone: true,
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: "./grid-config-card.component.html",
  styleUrl: "./grid-config-card.component.scss"
})
export class GridConfigCardComponent {
  @Input({ required: true }) public group!: FormGroup;

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public get showsAnchorPrice(): boolean {
    return this.group.get("entryMode")?.value === "manual";
  }
}