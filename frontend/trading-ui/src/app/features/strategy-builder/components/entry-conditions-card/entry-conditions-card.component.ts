import { Component } from "@angular/core";
import { MatCardModule } from "@angular/material/card";

@Component({
  selector: "app-entry-conditions-card",
  standalone: true,
  imports: [MatCardModule],
  templateUrl: "./entry-conditions-card.component.html",
  styleUrl: "./entry-conditions-card.component.scss"
})
export class EntryConditionsCardComponent {}