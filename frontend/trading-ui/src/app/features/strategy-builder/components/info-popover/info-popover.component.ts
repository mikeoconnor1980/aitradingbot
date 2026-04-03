import { Component, Input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatMenuModule } from "@angular/material/menu";

@Component({
  selector: "app-info-popover",
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatMenuModule],
  templateUrl: "./info-popover.component.html",
  styleUrl: "./info-popover.component.scss"
})
export class InfoPopoverComponent {
  @Input({ required: true }) public title!: string;
  @Input({ required: true }) public description!: string;

  public get ariaLabel(): string {
    return `Learn more about ${this.title}`;
  }
}