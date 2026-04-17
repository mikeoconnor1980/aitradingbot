import { Component } from "@angular/core";
import { MatTabsModule } from "@angular/material/tabs";
import { CandleManagementComponent } from "../candle-management/candle-management.component";
import { FearGreedManagementComponent } from "../fear-greed-management/fear-greed-management.component";

@Component({
  selector: "app-data-management",
  standalone: true,
  imports: [MatTabsModule, CandleManagementComponent, FearGreedManagementComponent],
  templateUrl: "./data-management.component.html",
  styleUrl: "./data-management.component.scss",
})
export class DataManagementComponent {}
