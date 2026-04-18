import { FormControl } from "@angular/forms";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { TagSelectorComponent } from "./tag-selector.component";

describe("TagSelectorComponent", () => {
  let fixture: ComponentFixture<TagSelectorComponent>;
  let component: TagSelectorComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TagSelectorComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TagSelectorComponent);
    component = fixture.componentInstance;
    component.control = new FormControl<string[]>([]);
    component.availableTags = ["trend", "ema", "range"];
    fixture.detectChanges();
  });

  it("should add and remove tags through the shared control", () => {
    component.toggleTag("trend");
    component.toggleTag("ema");

    expect(component.control?.value).toEqual(["trend", "ema"]);

    component.toggleTag("trend");

    expect(component.control?.value).toEqual(["ema"]);
  });

  it("should clear all selected tags", () => {
    component.control?.setValue(["trend", "ema"]);

    component.clearTags();

    expect(component.control?.value).toEqual([]);
  });
});