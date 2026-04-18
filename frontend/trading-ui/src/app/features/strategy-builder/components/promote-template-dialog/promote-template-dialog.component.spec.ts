import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import {
  PromoteTemplateDialogComponent,
  PromoteTemplateDialogData,
} from "./promote-template-dialog.component";

describe("PromoteTemplateDialogComponent", () => {
  let fixture: ComponentFixture<PromoteTemplateDialogComponent>;
  let component: PromoteTemplateDialogComponent;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<PromoteTemplateDialogComponent>>;

  const data: PromoteTemplateDialogData = {
    defaultName: "Mean Reversion Alpha",
    existingNames: ["Trend Pullback EMA Long"],
    availableTags: ["trend", "ema", "range"],
    initialTags: ["range"],
  };

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj("MatDialogRef", ["close"]);

    await TestBed.configureTestingModule({
      imports: [PromoteTemplateDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PromoteTemplateDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should prevent submit when the library name already exists", () => {
    component.form.patchValue({ name: "Trend Pullback EMA Long", description: "Duplicate" });

    component.onPromote();

    expect(component.form.get("name")?.hasError("duplicateName")).toBeTrue();
    expect(dialogRefSpy.close).not.toHaveBeenCalled();
  });

  it("should close with trimmed promotion data when valid", () => {
    component.form.patchValue({
      name: "  Range Reversion Beta  ",
      description: "  Shared mean reversion setup.  ",
      tags: ["range", "ema", "range"],
    });

    component.onPromote();

    expect(dialogRefSpy.close).toHaveBeenCalledWith({
      name: "Range Reversion Beta",
      description: "Shared mean reversion setup.",
      tags: ["range", "ema"],
    });
  });
});