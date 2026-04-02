import { Component, Input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { StrategyConfig } from "../../models/strategy.model";

@Component({
  selector: "app-json-preview-card",
  standalone: true,
  imports: [MatCardModule, MatButtonModule],
  templateUrl: "./json-preview-card.component.html",
  styleUrl: "./json-preview-card.component.scss"
})
export class JsonPreviewCardComponent {
  @Input() public config: StrategyConfig | null = null;

  public show = false;

  public toggle(): void {
    this.show = !this.show;
  }

  public get formattedJson(): string {
    return JSON.stringify(this.config, null, 2);
  }
}