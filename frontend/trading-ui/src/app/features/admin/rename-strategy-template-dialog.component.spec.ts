import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { RenameStrategyTemplateDialogComponent } from "./rename-strategy-template-dialog.component";

describe("RenameStrategyTemplateDialogComponent", () => {
  let fixture: ComponentFixture<RenameStrategyTemplateDialogComponent>;
  let component: RenameStrategyTemplateDialogComponent;
  let dialogRef: jasmine.SpyObj<MatDialogRef<RenameStrategyTemplateDialogComponent>>;

  beforeEach(async () => {
    dialogRef = jasmine.createSpyObj<MatDialogRef<RenameStrategyTemplateDialogComponent>>("MatDialogRef", ["close"]);

    await TestBed.configureTestingModule({
      imports: [RenameStrategyTemplateDialogComponent],
      providers: [
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            name: "Momentum Library",
            description: "Template description.",
            existingNames: ["Existing Template"]
          }
        },
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RenameStrategyTemplateDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("rejects duplicate names", () => {
    component.form.controls.name.setValue("Existing Template");

    expect(component.form.controls.name.hasError("duplicateName")).toBeTrue();
  });

  it("closes with trimmed values when valid", () => {
    component.form.controls.name.setValue("  Updated Template  ");
    component.form.controls.description.setValue("  Updated description.  ");

    component.onSave();

    expect(dialogRef.close).toHaveBeenCalledWith({
      name: "Updated Template",
      description: "Updated description."
    });
  });
});