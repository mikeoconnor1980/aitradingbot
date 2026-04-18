import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { StrategyTemplateDto } from "../../../models/strategy.model";
import { StrategyTemplateCardComponent } from "../../../components/strategy-template-card/strategy-template-card.component";
import { TemplateEducation, WizardEducationService } from "../../services/wizard-education.service";

@Component({
  selector: "app-wizard-goal-step",
  standalone: true,
  imports: [MatButtonModule, MatCardModule, MatChipsModule, MatIconModule, MatProgressSpinnerModule, StrategyTemplateCardComponent],
  templateUrl: "./wizard-goal-step.component.html",
  styleUrl: "./wizard-goal-step.component.scss"
})
export class WizardGoalStepComponent {
  @Input() public selectedTemplateId = "grid";
  @Input() public libraryTemplates: StrategyTemplateDto[] = [];
  @Input() public isLoadingLibrary = false;
  @Output() public readonly templateSelected = new EventEmitter<string>();
  @Output() public readonly libraryTemplateSelected = new EventEmitter<StrategyTemplateDto>();

  public readonly templates: TemplateEducation[];
  public showFullLibrary = false;
  public activeFilterTag: string | null = null;
  public activeFilterMode: string | null = null;

  public constructor() {
    const education = new WizardEducationService();
    this.templates = education.templates;
  }

  public get featuredTemplates(): StrategyTemplateDto[] {
    return this.libraryTemplates.slice(0, 6);
  }

  public get filteredLibraryTemplates(): StrategyTemplateDto[] {
    let filtered = this.libraryTemplates;

    if (this.activeFilterTag) {
      filtered = filtered.filter((t) => t.tags.includes(this.activeFilterTag!));
    }

    if (this.activeFilterMode) {
      filtered = filtered.filter((t) => t.strategyMode === this.activeFilterMode);
    }

    return filtered;
  }

  public get allTags(): string[] {
    const tags = new Set<string>();

    for (const t of this.libraryTemplates) {
      for (const tag of t.tags) {
        tags.add(tag);
      }
    }

    return Array.from(tags).sort();
  }

  public get allModes(): string[] {
    const modes = new Set<string>();

    for (const t of this.libraryTemplates) {
      modes.add(t.strategyMode);
    }

    return Array.from(modes).sort();
  }

  public selectTemplate(template: TemplateEducation): void {
    if (!template.available) {
      return;
    }

    this.templateSelected.emit(template.id);
  }

  public selectLibraryTemplate(template: StrategyTemplateDto): void {
    this.libraryTemplateSelected.emit(template);
  }

  public toggleFullLibrary(): void {
    this.showFullLibrary = !this.showFullLibrary;
    this.activeFilterTag = null;
    this.activeFilterMode = null;
  }

  public toggleFilterTag(tag: string): void {
    this.activeFilterTag = this.activeFilterTag === tag ? null : tag;
  }

  public toggleFilterMode(mode: string): void {
    this.activeFilterMode = this.activeFilterMode === mode ? null : mode;
  }
}
