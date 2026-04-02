import { Component } from "@angular/core";
import { MatCardModule } from "@angular/material/card";

@Component({
  selector: "app-trend-filter-card",
  standalone: true,
  imports: [MatCardModule],
  templateUrl: "./trend-filter-card.component.html",
  styleUrl: "./trend-filter-card.component.scss"
})
export class TrendFilterCardComponent {}