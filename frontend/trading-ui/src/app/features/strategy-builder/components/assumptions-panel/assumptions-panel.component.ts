import { CommonModule } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { AssumptionDto } from "../../models/strategy-intent.model";

@Component({
  selector: "app-assumptions-panel",
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: "./assumptions-panel.component.html",
  styleUrl: "./assumptions-panel.component.scss"
})
export class AssumptionsPanelComponent {
  @Input() public assumptions: AssumptionDto[] = [];
  @Output() public readonly editField = new EventEmitter<string>();

  public onEdit(fieldName: string): void {
    this.editField.emit(fieldName);
  }
}