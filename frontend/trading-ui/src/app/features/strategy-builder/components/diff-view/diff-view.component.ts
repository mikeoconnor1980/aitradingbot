import { Component, Input } from "@angular/core";
import { StrategyDiffDto } from "../../models/strategy.model";

@Component({
  selector: "app-diff-view",
  standalone: true,
  templateUrl: "./diff-view.component.html",
  styleUrl: "./diff-view.component.scss"
})
export class DiffViewComponent {
  @Input({ required: true })
  public diff!: StrategyDiffDto;
}