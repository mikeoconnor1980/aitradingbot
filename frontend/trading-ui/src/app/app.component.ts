import { Component } from "@angular/core";
import { StatusCardComponent } from "./features/connection/status-card.component";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [StatusCardComponent],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss"
})
export class AppComponent {
  public title = "Hyperliquid POC";
}
