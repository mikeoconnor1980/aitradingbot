import { CommonModule } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";

@Component({
  selector: "app-confidence-badge",
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: "./confidence-badge.component.html",
  styleUrl: "./confidence-badge.component.scss"
})
export class ConfidenceBadgeComponent {
  @Input({ required: true }) public confidence = 0;
  @Input() public clarificationNeeded: string | null = null;

  public get level(): "high" | "medium" | "low" {
    if (this.confidence >= 0.8) {
      return "high";
    }

    if (this.confidence >= 0.5) {
      return "medium";
    }

    return "low";
  }

  public get label(): string {
    const percentage = Math.round(this.confidence * 100);
    const prefix = this.level.charAt(0).toUpperCase() + this.level.slice(1);
    return `${prefix}: ${percentage}% confidence`;
  }

  public get icon(): string {
    switch (this.level) {
      case "high":
        return "check_circle";
      case "medium":
        return "info";
      default:
        return "warning";
    }
  }
}