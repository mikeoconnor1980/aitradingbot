import { AbstractControl } from "@angular/forms";
import { Component, Input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";

@Component({
  selector: "app-tag-selector",
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  templateUrl: "./tag-selector.component.html",
  styleUrl: "./tag-selector.component.scss"
})
export class TagSelectorComponent {
  @Input() public control: AbstractControl | null = null;
  @Input() public availableTags: string[] = [];
  @Input() public title = "Tags";
  @Input() public subtitle = "Select any tags that apply.";
  @Input() public emptyText = "No tags available yet.";

  public get selectedTags(): string[] {
    const value = this.control?.value;

    return Array.isArray(value)
      ? value.map((tag) => String(tag)).filter((tag) => tag.trim().length > 0)
      : [];
  }

  public get hasTags(): boolean {
    return this.availableTags.length > 0;
  }

  public isSelected(tag: string): boolean {
    return this.selectedTags.includes(tag);
  }

  public toggleTag(tag: string): void {
    if (this.control === null || this.control.disabled) {
      return;
    }

    const nextTags = this.isSelected(tag)
      ? this.selectedTags.filter((selectedTag) => selectedTag !== tag)
      : [...this.selectedTags, tag];

    this._setTags(nextTags);
  }

  public clearTags(): void {
    if (this.control === null || this.control.disabled || this.selectedTags.length === 0) {
      return;
    }

    this._setTags([]);
  }

  private _setTags(tags: string[]): void {
    const normalizedTags = tags
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0)
      .filter((tag, index, allTags) => allTags.indexOf(tag) === index);

    this.control?.setValue(normalizedTags);
    this.control?.markAsDirty();
    this.control?.markAsTouched();
    this.control?.updateValueAndValidity();
  }
}